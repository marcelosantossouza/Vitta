# Backlog do Vitta

Melhorias combinadas e ainda não feitas. Ordem dentro de cada bloco = prioridade sugerida.

## Prospecção

Feito: importação como lote (histórico + excluir), repetidos com opção de completar, campos de perfil e renda
(endereço, cidade, UF, CEP, telefone 2, nascimento, profissão/órgão, salário, descontos, empréstimos), lista com
filtros e ações em lote, dashboard com os mesmos filtros, CPF no prospect, filtro por CPF/CNPJ, funil paginado
(30 cards por etapa + "Mostrar mais", filtros aplicados no servidor).

1. **Cuidado LGPD:** salário, descontos e empréstimos são dados pessoais financeiros. Registrar a origem de cada lista
   (base legal) e limitar quem vê esses campos (ex.: perfil Assistente sem acesso a dados financeiros).
2. **Campos personalizados** também na prospecção (colunas sem campo próprio hoje vão para "Outras informações",
   visíveis mas não filtráveis).
3. Exportar a lista filtrada (planilha) e filtros salvos ("minhas listas").

### Leads de empresas (dados abertos do CNPJ)

Feito: `scripts/cnpj-leads.py` (baixa a base da Receita, filtra por UF/cidade/CNAE/porte, exclui MEI e empresário
individual, gera planilha que o Vitta importa sozinho) + campos de empresa e filtros Tipo/Porte na prospecção.

1. Atalhos de segmentos para vida em grupo (setores com convenção coletiva que costuma exigir seguro: construção,
   vigilância, limpeza, transporte...) em vez de digitar códigos CNAE.
2. Consulta avulsa de CNPJ no formulário do prospect (completar os dados de uma empresa na hora).
3. Rodar a busca pela tela do Vitta (hoje é um script no computador, por causa do tamanho da base: ~7,6 GB).

### Leads de pessoas (com consentimento)

Feito: página pública `/simular` (liga em Configurações → Página de captação). A pessoa pede contato e autoriza;
entra no funil com origem (`?origem=`), autorização registrada e contato no próximo dia útil. Anti-robô e limite de envios.

1. **Formulários de anúncio (Meta Lead Ads):** receber os leads do Facebook/Instagram direto no funil (webhook).
2. **Link de indicação:** cada cliente com um link próprio (`?origem=indicacao-nome`) e relatório de quem mais indica.
3. Simulação de preço na página (precisa da tabela de produtos MAG).
4. Pedido de exclusão de dados pelo próprio lead (LGPD art. 18).

### Indicações

Feito: grupos de indicação (pessoa, empresa, sindicato ou código), carimbo dos segurados, relatório mensal com
percentual de nome livre e PDF.

1. Acesso do parceiro (login só com o painel dele), se o formato do repasse for confirmado com o contador.
2. Vincular prospects à indicação e levar o vínculo na conversão em cliente.

### Site da corretora

Feito: home page pública (Configurações → Site da corretora) com logo, capa, sobre, produtos, contato, redes e SUSEP;
abre na raiz do domínio configurado na tela (HTTPS sob demanda pelo Caddy) ou em `/corretora`.

## Carteira

1. **Campos personalizados** (catálogo de campos com nome, tipo e descrição + valores em JSONB), para que colunas
   específicas de cada seguradora apareçam na tela, com filtro e ordenação, em vez de irem para "Outras informações".
2. Saídas da carteira encerrarem automaticamente a apólice cadastrada (com o motivo), em vez de só aparecerem na lista.

## Usuários e acesso

1. **"Esqueci minha senha"** por e-mail (link com validade curta). O envio de e-mail já existe.
2. **Multi-corretora:** cada dado pertence a uma corretora; configurações (IA, e-mail diário) passam a ser por corretora.
   Obrigatório antes de vender para uma segunda corretora.

## Produto

1. **Idiomas (pt-BR e en, depois outros):** tirar os textos fixos das telas e dos e-mails (i18n do Angular).
2. **App mobile:** trazer as telas novas (comissão, configurações, usuários).
3. Excluir os dados de demonstração do banco de desenvolvimento.
4. Atualizar o `scripts/smoke_test.py` (ainda espera "Nº da apólice" obrigatório).

## Infraestrutura

1. Quando a máquina ARM (A1) da Oracle sair: migrar (backup → deploy na A1 → restaurar → trocar o IP no DuckDNS).
2. Configurar no servidor: e-mail diário (SMTP) e cópia do backup para o Google Drive.
