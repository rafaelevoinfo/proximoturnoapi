# Chat de regras: jogo em foco da conversa

Só `proximoturnoapi`. O front já manda `idJogoPagina` em toda pergunta e não muda.

## Problema

O assistente erra com frequência o jogo da pergunta, e cada erro é uma busca no Qdrant (e um
embedding) gasta no manual errado. A causa não é falta de capacidade do modelo, é falta de
informação em dois pontos do `ResponderPerguntaRegras`:

1. **A memória perde o jogo resolvido.** `SoConversa` guarda só o texto do assistente; as
   chamadas de `listar_jogos`/`buscar_regras` (e com elas o `idJogo` escolhido) ficam de fora.
   Como a regra 9 proíbe mostrar ids, e a resposta nem sempre cita o nome do jogo, na pergunta
   seguinte ("e no fim da rodada?") o modelo não sabe mais de que jogo falava.
2. **O contexto da página vence a troca de jogo.** O front manda `idJogoPagina` em toda
   pergunta, e a instrução diz "se o usuário estiver na página de um jogo e não citar outro, use
   esse". Quem abriu o chat em Azul, perguntou de Catan e depois mandou "e o ladrão?" volta a
   ser respondido com o manual de Azul. O texto ainda diz "abriu o chat na página", mesmo
   quando o usuário só navegou para lá no meio da conversa.

## Alternativa avaliada: LLM classificador a cada pergunta não óbvia

Uma chamada extra, antes do agente, que decide o jogo. Funciona, mas:

- o classificador precisa do mesmo que falta ao agente (histórico com o jogo atual e o
  catálogo); dado isso ao agente, a chamada extra deixa de ser necessária;
- soma latência antes do primeiro texto e custo em toda pergunta de seguimento, que é a
  maioria;
- "óbvio só quando abre na página" é estreito demais: depois que o jogo foi resolvido uma vez,
  ele também é óbvio até o usuário citar outro.

Fica como fallback possível (item 4), não como caminho principal.

> Atualização: o classificador avaliado é o Jev (TypeSafe), que custa uma fração de um LLM e
> responde mais rápido, o que derruba as objeções de custo e latência. Mesmo assim ele precisa
> do jogo em foco para decidir, então os itens 1 e 2 entram primeiro, nesta entrega. Os itens 3
> e 4 (ou o Jev no lugar deles) ficam para depois de medir.

## Desenho

### 1. Jogo em foco é estado da conversa, não memória do modelo

Coluna nova `CHAT_CONVERSA.ID_JOGO_FOCO`. O `ID_JOGO` continua sendo o jogo da página em que a
conversa foi aberta, que o admin mostra como "Aberta na página de…". O foco:

- conversa nova: o jogo da página, se houver; conversa ainda sem foco: a página da pergunta;
- a cada `buscar_regras` bem-sucedida, o jogo buscado vira o foco (o modelo trocou de jogo);
- gravado junto com a sessão, no fim do turno; pergunta cancelada não muda o foco.

Migração `ChatConversaJogoFoco`: adiciona a coluna e copia `ID_JOGO` para as conversas em
andamento. Busca recusada (jogo sem manual ou inexistente) não muda o foco. O gasto do chat em
`USO_LLM` passa a ir para o jogo em foco.

### 2. Contexto do turno com foco e página separados

`Contexto(...)` passa a descrever as duas coisas, em vez de uma frase só da página:

```
Jogo em foco na conversa: Catan (id 12).
O usuário está agora na página de: Azul (id 7).
```

E a regra 3 vira, em ordem:

1. se a mensagem cita um jogo, é esse (ver item 3);
2. senão, o jogo em foco;
3. senão (conversa sem foco), o jogo da página em que o usuário está;
4. senão, pergunte.

A página só define o jogo quando a conversa ainda não tem foco. Navegar não troca o assunto;
citar outro jogo troca.

### 3. Casamento determinístico de nomes antes do modelo

Um `IdentificadorJogo` (puro, testável, sem LLM) procura no catálogo os jogos citados na
mensagem: normaliza caixa e acentos, compara por palavras do nome (ignorando artigos e
subtítulo depois de `:`/`-`) e tolera erro de digitação pequeno (distância de edição ≤ 1 em
palavras de 5+ letras). Resultado:

- **um jogo**: entra no contexto como "a mensagem cita: Catan (id 12)" e o modelo nem precisa
  de `listar_jogos`;
- **vários**: entram como candidatos, e o modelo pergunta qual, ou busca em cada um se a dúvida
  envolver mais de um;
- **nenhum**: vale o foco/página; `listar_jogos` continua disponível para apelidos que o
  casamento não pega.

O catálogo já é lido por pergunta em `FerramentasChat`; o identificador usa a mesma leitura.

### 4. (Opcional, depois de medir) classificador LLM só no caso ambíguo

Se a auditoria mostrar que ainda há erro quando não há foco nem nome casado, uma chamada curta
com modelo barato e saída estruturada (`{ idJogo | null, confiança }`), só nesse caso. Não entra
nesta entrega.

## Medição

`CHAT_MENSAGEM.TRECHOS` já registra cada busca com `IdJogo`. Para comparar antes/depois, o
turno passa a registrar também o foco no início do turno e a origem do jogo (`nome`, `foco`,
`pagina`, `modelo`). Erro de jogo aparece como turno com buscas em mais de um jogo sem a
mensagem citar dois, ou como resposta "não encontrei no manual" seguida de troca de jogo.

## Testes (`ResponderPerguntaRegrasTests` + novo `IdentificadorJogoTests`)

- conversa aberta na página de A, pergunta sobre B, seguimento sem nome → contexto aponta B;
- usuário navega para a página de C no meio da conversa com foco em B → foco continua B;
- conversa nova fora de página, sem nome → sem foco, modelo pergunta;
- `buscar_regras` em B muda o foco; pergunta cancelada não muda;
- identificador: acento, caixa, subtítulo, erro de uma letra, nome contido em outro
  ("Catan" vs "Catan: Cidades e Cavaleiros" → os dois candidatos), palavra comum que não é jogo.
