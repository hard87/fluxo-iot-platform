# Contribuindo com o Fluxo

Obrigado por contribuir com o Fluxo.

## Objetivo deste repositorio

Este projeto e um portfolio tecnico com foco em:
- engenharia de software em .NET com Clean Architecture;
- pipeline IoT com MQTT + Worker + PostgreSQL;
- seguranca aplicada ao MVP;
- evolucao para piloto e escala.

## Fluxo de trabalho recomendado

1. Crie uma branch a partir de `main`.
2. Faça alteracoes pequenas e com escopo claro.
3. Rode build e testes localmente.
4. Abra PR com evidencias tecnicas.

Exemplo de nomes de branch:
- `feat/...`
- `fix/...`
- `test/...`
- `chore/...`
- `docs/...`

## Padrao de qualidade minimo

Antes da PR:

```powershell
dotnet build Fluxo.slnx
dotnet test Fluxo.slnx
```

Se alterar `portal-web`:

```powershell
cd portal-web
npm install
npm run build
```

## Escopo e estilo

- Prefira PRs pequenas e objetivas.
- Atualize documentacao quando alterar comportamento.
- Nao versione segredos locais (`.env`, credenciais MQTT, chaves).
- Mantenha consistencia com os guias em `docs/`.

## Pull Request

Use o template de PR em `.github/PULL_REQUEST_TEMPLATE.md`.

Campos esperados:
- contexto e objetivo;
- o que mudou;
- como validar;
- riscos e limitacoes;
- checklist tecnico.

## PRs empilhados (stacked PRs)

Regra: **todo PR que pode receber merge precisa ter `main` como base.** Um PR cuja base é outra
branch (não `main`) é considerado empilhado e não pode ser mergeado enquanto essa base não mudar.

- Se o seu trabalho depende de um PR ainda não integrado a `main`, abra o PR filho com base na
  branch do PR pai, mas **marque-o como Draft** e escreva `Depends on #NN` no corpo (ou aplique a
  label `stacked-pr`, quando disponível). Isso permite revisão antecipada sem risco de merge
  acidental.
- Um check obrigatório (`pr-policy`, ver `.github/workflows/pr-policy.yml`) falha automaticamente
  se um PR sair de Draft ("Ready for review") com uma base diferente de `main` — é o gate técnico
  que impede o incidente de um PR aparecer como "Merged" no GitHub sem que seu conteúdo chegue a
  `main`.
- Depois que o PR pai for mergeado em `main`:
  1. atualize sua branch sobre `main` (`git rebase main` ou `git merge main`, conforme o histórico
     do repositório);
  2. mude a base do PR filho para `main` na interface do GitHub;
  3. deixe o CI rodar de novo sobre a base atualizada;
  4. peça (ou aguarde) aprovação;
  5. só então marque o PR como "Ready for review" — o check falha antes disso se a base ainda não
     for `main`.
- Nunca declare uma etapa concluída em documentação (`docs/project-status.md` ou equivalente) só
  porque um PR aparece como "Merged" — confirme que o conteúdo está no HEAD real de `main`.
- Como rede de segurança (não como substituto do `pr-policy`), `.github/workflows/post-merge-check.yml`
  roda a cada PR fechado como "Merged": se a base não era `main`, abre automaticamente uma issue de
  incidente com label `merge-not-in-main` listando o que ainda falta chegar a `main`.

## Tamanho e conteúdo de PRs

- Até 500 linhas alteradas: fluxo normal.
- 501–1000 linhas: inclua no corpo do PR um resumo por componente do que mudou.
- Acima de 1000 linhas: `pr-policy` (ver `.github/workflows/pr-policy.yml`) emite um aviso pedindo
  justificativa e plano de decomposição no corpo do PR — não bloqueia o merge, porque migrations,
  lockfiles e reconciliações legítimas podem exceder o limite. O aviso existe para o revisor saber
  o motivo, não para impedir o PR.
- Arquivos gerados (build output, lockfiles de dependência) devem ser identificados no corpo do PR,
  mas não contam como código revisável.
- JSONs volumosos, screenshots e relatórios de execução de teste vão para artifacts do GitHub
  Actions ou releases — não para o histórico do Git. Binários no repositório só com justificativa
  explícita no corpo do PR.

## Responsáveis por área (CODEOWNERS)

`.github/CODEOWNERS` mapeia área do código a responsável técnico. Hoje, com um único mantenedor,
esse arquivo é só um mapa de responsabilidade — a branch protection de `main` não exige aprovação
de code owner. Isso deve ser ativado quando houver um segundo revisor válido.
