# Feature: Importacao de carteira (proximo passo)

Fluxo planejado:

1. Usuario faz upload da planilha mensal da seguradora (ex.: MAG) + mes de referencia.
2. `ImportPortfolioCommand` escolhe o `IPortfolioSpreadsheetReader` pelo codigo da seguradora.
3. Le as linhas atuais, busca as linhas da importacao anterior e chama `PortfolioReconciler.Reconcile`.
4. Persiste `PortfolioImport` + `PortfolioChange` (Entrou / Saiu / Mudou).
5. (Opcional) `IAiService.SummarizePortfolioChangesAsync` gera um resumo para o dashboard.
6. Saidas viram alertas para o corretor investigar (cancelamento? troca de corretor?).

Precisamos de um exemplo real (anonimizado) da planilha da MAG para mapear as colunas.
