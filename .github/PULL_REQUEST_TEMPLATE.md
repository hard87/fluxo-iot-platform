## Contexto

Descreva o problema e o objetivo desta PR.

## Base e dependencias

- [ ] A base final deste PR e `main`
- [ ] Este PR nao depende de nenhum outro PR ainda nao mergeado em `main`
- [ ] Se empilhado sobre outro PR, este PR permanece em Draft e declara `Depends on #...` abaixo

Depends on: _(numero do PR pai, se houver)_

## O que mudou

- 
- 
- 

## Como validar

1. 
2. 
3. 

Comandos:

```powershell
dotnet build Fluxo.slnx
dotnet test Fluxo.slnx
```

Se aplicavel ao frontend:

```powershell
cd portal-web
npm run build
```

## Evidencias

- [ ] Build local ok
- [ ] Testes locais ok
- [ ] Fluxo funcional validado (quando aplicavel)

## Riscos e limitacoes

- 

## Checklist

- [ ] Escopo da PR esta claro e objetivo
- [ ] Nao inclui segredos ou credenciais
- [ ] Documentacao foi atualizada quando necessario
- [ ] O diff exibido nesta PR e o diff que chegara a `main` (base correta, sem branch intermediaria)
