import { useCallback, useEffect, useRef, useState } from "react";
import type { FormEvent } from "react";
import { Link, useLocation, useNavigate, useParams } from "react-router-dom";
import { EmptyState, ErrorState, LoadingState } from "../components/feedback/FeedbackStates";
import { PageHeader } from "../components/PageHeader";
import { useAuth } from "../hooks/useAuth";
import * as alertRulesService from "../services/api/alertRulesService";
import type { SaveAlertRulePayload } from "../services/api/alertRulesService";
import * as deviceService from "../services/api/deviceService";
import { ApiError } from "../services/api/httpClient";
import * as telemetryService from "../services/api/telemetryService";
import type { AlertOperator, AlertRuleRevision, AlertSeverity, DeviceResponse, MetricDefinitionResponse } from "../types";
import { getApiErrorMessage } from "../utils/apiErrorMessage";
import { sanitizeText } from "../utils/sanitize";

type FieldName =
  | "name"
  | "metricDefinitionId"
  | "operator"
  | "threshold"
  | "thresholdHigh"
  | "hysteresis"
  | "durationSeconds"
  | "cooldownSeconds";

const NUMERIC_OPERATORS: AlertOperator[] = [
  "GreaterThan",
  "GreaterOrEqual",
  "LessThan",
  "LessOrEqual",
  "InsideRange",
  "OutsideRange"
];
const BOOLEAN_OPERATORS: AlertOperator[] = ["IsTrue", "IsFalse"];

const operatorLabels: Record<AlertOperator, string> = {
  GreaterThan: "Maior que",
  GreaterOrEqual: "Maior ou igual a",
  LessThan: "Menor que",
  LessOrEqual: "Menor ou igual a",
  InsideRange: "Dentro do intervalo",
  OutsideRange: "Fora do intervalo",
  IsTrue: "É verdadeiro",
  IsFalse: "É falso"
};

function isBooleanOperator(operator: AlertOperator | ""): boolean {
  return operator === "IsTrue" || operator === "IsFalse";
}

function isRangeOperator(operator: AlertOperator | ""): boolean {
  return operator === "InsideRange" || operator === "OutsideRange";
}

// 20 pages * ALERTS_PAGE_SIZE (100) covers far more rules than this MVP phase expects; there is
// no dedicated "get one rule" endpoint, so editing looks the current revision up by scanning pages.
const MAX_RULE_LOOKUP_PAGES = 20;

async function findRuleById(
  token: string,
  workspaceId: string,
  ruleId: string
): Promise<AlertRuleRevision | null> {
  for (let page = 1; page <= MAX_RULE_LOOKUP_PAGES; page += 1) {
    const items = await alertRulesService.listAlertRules(token, workspaceId, page);
    const found = items.find((item) => item.ruleId === ruleId);
    if (found) {
      return found;
    }
    if (!alertRulesService.hasPossibleNextAlertsPage(items)) {
      break;
    }
  }
  return null;
}

export function AlertRuleFormPage() {
  const { workspaceId, ruleId } = useParams<{ workspaceId: string; ruleId?: string }>();
  const navigate = useNavigate();
  const location = useLocation();
  const { token } = useAuth();
  const isEditMode = Boolean(ruleId);

  const isMountedRef = useRef(true);
  useEffect(() => {
    isMountedRef.current = true;
    return () => {
      isMountedRef.current = false;
    };
  }, []);

  const [metrics, setMetrics] = useState<MetricDefinitionResponse[]>([]);
  const [devices, setDevices] = useState<DeviceResponse[]>([]);
  const [loadingOptions, setLoadingOptions] = useState(true);
  const [optionsError, setOptionsError] = useState<unknown>(null);

  const [existingRule, setExistingRule] = useState<AlertRuleRevision | null>(null);
  const [loadingRule, setLoadingRule] = useState(isEditMode);
  const [ruleError, setRuleError] = useState<unknown>(null);
  const [ruleNotFound, setRuleNotFound] = useState(false);

  const [name, setName] = useState("");
  const [metricDefinitionId, setMetricDefinitionId] = useState("");
  const [deviceIdentifier, setDeviceIdentifier] = useState("");
  const [operator, setOperator] = useState<AlertOperator | "">("");
  const [threshold, setThreshold] = useState("");
  const [thresholdHigh, setThresholdHigh] = useState("");
  const [hysteresis, setHysteresis] = useState("0");
  const [durationSeconds, setDurationSeconds] = useState("0");
  const [cooldownSeconds, setCooldownSeconds] = useState("0");
  const [severity, setSeverity] = useState<AlertSeverity>("Warning");
  const [enabled, setEnabled] = useState(false);

  const [fieldErrors, setFieldErrors] = useState<Partial<Record<FieldName, string>>>({});
  const [showActiveConfirm, setShowActiveConfirm] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [submitError, setSubmitError] = useState<unknown>(null);

  const loadOptions = useCallback(async (authToken: string, wsId: string) => {
    setLoadingOptions(true);
    setOptionsError(null);
    try {
      const [metricDefinitions, deviceList] = await Promise.all([
        telemetryService.listMetricDefinitions(authToken, wsId),
        deviceService.listDevices(authToken, wsId)
      ]);
      if (isMountedRef.current) {
        setMetrics(metricDefinitions.filter((metric) => metric.valueType !== "Text" && metric.status !== "Ignored"));
        setDevices(deviceList.filter((device) => device.isActive));
      }
    } catch (err) {
      if (isMountedRef.current) {
        setOptionsError(err);
      }
    } finally {
      if (isMountedRef.current) {
        setLoadingOptions(false);
      }
    }
  }, []);

  useEffect(() => {
    if (!token || !workspaceId) {
      return;
    }
    void loadOptions(token, workspaceId);
  }, [token, workspaceId, loadOptions]);

  function applyRule(rule: AlertRuleRevision) {
    setExistingRule(rule);
    setName(rule.name);
    setMetricDefinitionId(rule.metricDefinitionId);
    setDeviceIdentifier(rule.deviceIdentifier ?? "");
    setOperator(rule.operator);
    setThreshold(rule.threshold !== null ? String(rule.threshold) : "");
    setThresholdHigh(rule.thresholdHigh !== null ? String(rule.thresholdHigh) : "");
    setHysteresis(String(rule.hysteresis));
    setDurationSeconds(String(rule.durationSeconds));
    setCooldownSeconds(String(rule.cooldownSeconds));
    setSeverity(rule.severity);
    setEnabled(rule.enabled);
  }

  const loadRule = useCallback(
    async (authToken: string, wsId: string, id: string) => {
      setLoadingRule(true);
      setRuleError(null);
      setRuleNotFound(false);
      try {
        const rule = await findRuleById(authToken, wsId, id);
        if (!isMountedRef.current) {
          return;
        }
        if (rule) {
          applyRule(rule);
        } else {
          setRuleNotFound(true);
        }
      } catch (err) {
        if (isMountedRef.current) {
          setRuleError(err);
        }
      } finally {
        if (isMountedRef.current) {
          setLoadingRule(false);
        }
      }
    },
    []
  );

  useEffect(() => {
    if (!isEditMode || !token || !workspaceId || !ruleId) {
      return;
    }

    const stateRule = (location.state as { rule?: AlertRuleRevision } | null)?.rule;
    if (stateRule && stateRule.ruleId === ruleId) {
      applyRule(stateRule);
      setLoadingRule(false);
      return;
    }

    void loadRule(token, workspaceId, ruleId);
  }, [isEditMode, token, workspaceId, ruleId, loadRule, location.state]);

  function handleMetricChange(newMetricId: string) {
    setMetricDefinitionId(newMetricId);
    const metric = metrics.find((item) => item.id === newMetricId);
    if (metric?.valueType === "Boolean") {
      setOperator("IsTrue");
      setThreshold("");
      setThresholdHigh("");
      setHysteresis("0");
    } else if (metric?.valueType === "Numeric") {
      setOperator("GreaterThan");
      setThresholdHigh("");
    } else {
      setOperator("");
    }
  }

  function handleOperatorChange(newOperator: AlertOperator) {
    setOperator(newOperator);
    if (!isRangeOperator(newOperator)) {
      setThresholdHigh("");
    }
    if (isBooleanOperator(newOperator)) {
      setThreshold("");
      setHysteresis("0");
    }
  }

  const selectedMetric = metrics.find((metric) => metric.id === metricDefinitionId) ?? null;
  const operatorOptions =
    selectedMetric?.valueType === "Boolean"
      ? BOOLEAN_OPERATORS
      : selectedMetric?.valueType === "Numeric"
        ? NUMERIC_OPERATORS
        : [];

  function validate(): Partial<Record<FieldName, string>> {
    const errors: Partial<Record<FieldName, string>> = {};
    const trimmedName = sanitizeText(name);

    if (!trimmedName) {
      errors.name = "Nome é obrigatório.";
    } else if (trimmedName.length > 120) {
      errors.name = "Nome deve ter no máximo 120 caracteres.";
    }

    if (!metricDefinitionId) {
      errors.metricDefinitionId = "Selecione uma métrica.";
    }

    if (!operator) {
      errors.operator = "Selecione uma condição.";
    }

    if (operator && !isBooleanOperator(operator)) {
      const parsedThreshold = Number(threshold);
      if (threshold.trim() === "" || Number.isNaN(parsedThreshold)) {
        errors.threshold = "Informe um valor numérico.";
      }

      if (isRangeOperator(operator)) {
        const parsedHigh = Number(thresholdHigh);
        if (thresholdHigh.trim() === "" || Number.isNaN(parsedHigh)) {
          errors.thresholdHigh = "Informe o limite superior.";
        } else if (!Number.isNaN(parsedThreshold) && parsedThreshold > parsedHigh) {
          errors.thresholdHigh = "O limite superior deve ser maior ou igual ao inferior.";
        }
      }

      const parsedHysteresis = Number(hysteresis || "0");
      if (Number.isNaN(parsedHysteresis) || parsedHysteresis < 0) {
        errors.hysteresis = "Informe um valor maior ou igual a zero.";
      }
    }

    const parsedDuration = Number(durationSeconds || "0");
    if (Number.isNaN(parsedDuration) || parsedDuration < 0) {
      errors.durationSeconds = "Informe um valor maior ou igual a zero.";
    }

    const parsedCooldown = Number(cooldownSeconds || "0");
    if (Number.isNaN(parsedCooldown) || parsedCooldown < 0) {
      errors.cooldownSeconds = "Informe um valor maior ou igual a zero.";
    }

    return errors;
  }

  function buildPayload(): SaveAlertRulePayload {
    const booleanOperator = isBooleanOperator(operator);
    const rangeOperator = isRangeOperator(operator);

    return {
      name: sanitizeText(name),
      metricDefinitionId,
      deviceIdentifier: deviceIdentifier || null,
      operator: operator as AlertOperator,
      threshold: booleanOperator ? null : Number(threshold),
      thresholdHigh: rangeOperator ? Number(thresholdHigh) : null,
      hysteresis: booleanOperator ? 0 : Number(hysteresis || "0"),
      durationSeconds: Number(durationSeconds || "0"),
      cooldownSeconds: Number(cooldownSeconds || "0"),
      severity,
      enabled: isEditMode ? enabled : false,
      expectedVersion: isEditMode && existingRule ? existingRule.version : undefined
    };
  }

  async function performSave() {
    if (!token || !workspaceId) {
      return;
    }
    setSubmitting(true);
    setSubmitError(null);
    const payload = buildPayload();
    try {
      if (isEditMode && existingRule) {
        await alertRulesService.updateAlertRule(token, workspaceId, existingRule.ruleId, payload);
      } else {
        await alertRulesService.createAlertRule(token, workspaceId, payload);
      }
      navigate(`/workspaces/${workspaceId}/alerts`);
    } catch (err) {
      if (isMountedRef.current) {
        setSubmitError(err);
        setShowActiveConfirm(false);
      }
    } finally {
      if (isMountedRef.current) {
        setSubmitting(false);
      }
    }
  }

  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const errors = validate();
    setFieldErrors(errors);
    if (Object.keys(errors).length > 0) {
      return;
    }

    if (isEditMode && existingRule?.enabled) {
      setShowActiveConfirm(true);
      return;
    }

    void performSave();
  }

  function submitErrorMessage(error: unknown): string {
    if (error instanceof ApiError && error.status === 409) {
      return "Esta regra foi alterada por outra ação enquanto você editava. Volte para a lista, abra novamente e refaça a edição.";
    }
    return getApiErrorMessage(error, "Ocorreu um erro inesperado ao salvar a regra.");
  }

  if (!isEditMode ? loadingOptions : loadingOptions || loadingRule) {
    return (
      <section>
        <PageHeader title={isEditMode ? "Editar regra de alerta" : "Nova regra de alerta"} />
        <LoadingState compact title="Carregando formulário" />
      </section>
    );
  }

  if (optionsError) {
    return (
      <section>
        <PageHeader title={isEditMode ? "Editar regra de alerta" : "Nova regra de alerta"} />
        <ErrorState
          title="Não foi possível carregar métricas e dispositivos"
          description={getApiErrorMessage(optionsError, "Ocorreu um erro inesperado ao carregar o formulário.")}
          action={
            token && workspaceId ? (
              <button type="button" className="button-secondary" onClick={() => void loadOptions(token, workspaceId)}>
                Tentar novamente
              </button>
            ) : undefined
          }
        />
      </section>
    );
  }

  if (isEditMode && ruleError) {
    return (
      <section>
        <PageHeader title="Editar regra de alerta" />
        <ErrorState
          title="Não foi possível carregar a regra"
          description={getApiErrorMessage(ruleError, "Ocorreu um erro inesperado ao carregar a regra.")}
          action={
            token && workspaceId && ruleId ? (
              <button type="button" className="button-secondary" onClick={() => void loadRule(token, workspaceId, ruleId)}>
                Tentar novamente
              </button>
            ) : undefined
          }
        />
      </section>
    );
  }

  if (isEditMode && ruleNotFound) {
    return (
      <section>
        <PageHeader title="Editar regra de alerta" />
        <EmptyState
          title="Regra não encontrada"
          description="A regra pode ter sido removida ou o link está incorreto."
          action={workspaceId ? <Link to={`/workspaces/${workspaceId}/alerts`}>Voltar para Alertas</Link> : undefined}
        />
      </section>
    );
  }

  return (
    <section>
      <PageHeader
        title={isEditMode ? "Editar regra de alerta" : "Nova regra de alerta"}
        description="O servidor valida e é autoritativo sobre todas as combinações; esta validação é apenas um apoio."
      />

      <form onSubmit={handleSubmit} className="form-grid panel">
        <label className={fieldErrors.name ? "field-error" : undefined}>
          Nome
          <input type="text" value={name} onChange={(event) => setName(event.target.value)} maxLength={120} />
          {fieldErrors.name ? <span className="field-error-message">{fieldErrors.name}</span> : null}
        </label>

        <label className={fieldErrors.metricDefinitionId ? "field-error" : undefined}>
          Métrica
          <select value={metricDefinitionId} onChange={(event) => handleMetricChange(event.target.value)}>
            <option value="">Selecione…</option>
            {metrics.map((metric) => (
              <option key={metric.id} value={metric.id}>
                {metric.displayName} ({metric.valueType})
              </option>
            ))}
          </select>
          {fieldErrors.metricDefinitionId ? (
            <span className="field-error-message">{fieldErrors.metricDefinitionId}</span>
          ) : null}
        </label>

        <label>
          Dispositivo
          <select value={deviceIdentifier} onChange={(event) => setDeviceIdentifier(event.target.value)}>
            <option value="">Todos os dispositivos compatíveis</option>
            {devices.map((device) => (
              <option key={device.id} value={device.identifier}>
                {device.name} ({device.identifier})
              </option>
            ))}
          </select>
        </label>

        <label className={fieldErrors.operator ? "field-error" : undefined}>
          Condição
          <select
            value={operator}
            onChange={(event) => handleOperatorChange(event.target.value as AlertOperator)}
            disabled={!selectedMetric}
          >
            <option value="">Selecione…</option>
            {operatorOptions.map((option) => (
              <option key={option} value={option}>
                {operatorLabels[option]}
              </option>
            ))}
          </select>
          {fieldErrors.operator ? <span className="field-error-message">{fieldErrors.operator}</span> : null}
        </label>

        {operator && !isBooleanOperator(operator) ? (
          <>
            <label className={fieldErrors.threshold ? "field-error" : undefined}>
              {isRangeOperator(operator) ? "Limite inferior" : "Limite"}
              <input
                type="number"
                value={threshold}
                onChange={(event) => setThreshold(event.target.value)}
                step="any"
              />
              {fieldErrors.threshold ? <span className="field-error-message">{fieldErrors.threshold}</span> : null}
            </label>

            {isRangeOperator(operator) ? (
              <label className={fieldErrors.thresholdHigh ? "field-error" : undefined}>
                Limite superior
                <input
                  type="number"
                  value={thresholdHigh}
                  onChange={(event) => setThresholdHigh(event.target.value)}
                  step="any"
                />
                {fieldErrors.thresholdHigh ? (
                  <span className="field-error-message">{fieldErrors.thresholdHigh}</span>
                ) : null}
              </label>
            ) : null}

            <label className={fieldErrors.hysteresis ? "field-error" : undefined}>
              Histerese
              <input
                type="number"
                min={0}
                step="any"
                value={hysteresis}
                onChange={(event) => setHysteresis(event.target.value)}
              />
              {fieldErrors.hysteresis ? <span className="field-error-message">{fieldErrors.hysteresis}</span> : null}
            </label>
          </>
        ) : null}

        <label className={fieldErrors.durationSeconds ? "field-error" : undefined}>
          Duração (segundos)
          <input
            type="number"
            min={0}
            value={durationSeconds}
            onChange={(event) => setDurationSeconds(event.target.value)}
          />
          {fieldErrors.durationSeconds ? (
            <span className="field-error-message">{fieldErrors.durationSeconds}</span>
          ) : null}
        </label>

        <label className={fieldErrors.cooldownSeconds ? "field-error" : undefined}>
          Cooldown (segundos)
          <input
            type="number"
            min={0}
            value={cooldownSeconds}
            onChange={(event) => setCooldownSeconds(event.target.value)}
          />
          {fieldErrors.cooldownSeconds ? (
            <span className="field-error-message">{fieldErrors.cooldownSeconds}</span>
          ) : null}
        </label>

        <label>
          Severidade
          <select value={severity} onChange={(event) => setSeverity(event.target.value as AlertSeverity)}>
            <option value="Info">Informativo</option>
            <option value="Warning">Aviso</option>
            <option value="Critical">Crítico</option>
          </select>
        </label>

        {isEditMode ? (
          <label className="checkbox-field">
            <input type="checkbox" checked={enabled} onChange={(event) => setEnabled(event.target.checked)} />
            Regra ativa
          </label>
        ) : (
          <p className="muted">Novas regras começam desativadas. Ative pela lista depois de revisar.</p>
        )}

        {existingRule ? (
          <p className="muted">
            Intervalo esperado atual: {existingRule.expectedIntervalSeconds}s (definido pela métrica, não é editável
            aqui).
          </p>
        ) : null}

        {submitError ? <ErrorState compact title="Não foi possível salvar" description={submitErrorMessage(submitError)} /> : null}

        {showActiveConfirm ? (
          <div className="panel" role="alertdialog" aria-label="Confirmar alteração de regra ativa">
            <p>
              Esta regra está ativa. Salvar cria uma nova revisão e fecha os eventos em aberto associados à revisão
              atual. Confirmar alteração?
            </p>
            <div className="inline-actions">
              <button type="button" disabled={submitting} onClick={() => void performSave()}>
                {submitting ? "Salvando…" : "Confirmar e salvar"}
              </button>
              <button type="button" className="button-secondary" onClick={() => setShowActiveConfirm(false)}>
                Cancelar
              </button>
            </div>
          </div>
        ) : (
          <div className="inline-actions">
            <button type="submit" disabled={submitting}>
              {submitting ? "Salvando…" : "Salvar regra"}
            </button>
            {workspaceId ? (
              <Link className="button-secondary" to={`/workspaces/${workspaceId}/alerts`}>
                Cancelar
              </Link>
            ) : null}
          </div>
        )}
      </form>
    </section>
  );
}
