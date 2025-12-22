# Descrição do Projeto
Esta será uma API para um web app de aluguel de jogos de tabuleiro. Nessa API as seguintes funcionalidades irão existir:
* CRUD de cliente.  
    * Cada cliente precisa minimamente dos campos:
        * Nome Completo
        * Telefone
        * Endereço
        * Email
        Campos opcionais:
            * Data de Nascimento
            * Como nos conheceu?
            * Aceita receber ofertas?

* CRUD de Jogo
    * Campos obrigatorios:
        * Nome
        * Descrição
        * Idade minima
        * Foto
        * Minimo de jogadores
        * Máximo de jogadores
        * Categoria (Categoria usada para saber o preço)
        * Tempo estimado por partida
        * Links para videos
        * Valor de Compra
        * Data de Aquisição
        * Tags (Lista de de nomes para encontrar o jogo facilmente. Ex. Facil de jogar, bom para crianças, estratégico etc)
        * Status (Alugado, Disponivel, Indisponível)
        Campos opcionais:  
            * Tempo estimado de jogo
            * Links para videos sobre o jogo
            * Valor de compra
            * Data de compra

* CRUD de Categoria
    * Campos obrigatorios
        * Descrição

* CRUD de Preço por Categoria
    * Campos obrigatórios
        * Id da categoria
        * Quantidade de dias
        * Valor

* CRUD de Pedido de Aluguel
    * Campos obrigatórios
        * Id do Cliente
        * Data         
        * Valor Total
        * Jogos (Tabela filha)
            * Id do jogo             
            * Valor  
            * Data de Devolução
            * Status (Alugado, Devolvido, Renovado)            

* Funcionalidades extras
    * Devolver jogos de um pedido
        Altera o status de todos os jogos de um pedido para disponível
        * Parametros de entrada
            * Id do Pedido
        

    * Devolver jogo avulso
        Altera o status do jogo informado para disponivel
        * Parametros de entrada
            * Id do Pedido
            * Id do jogo
        

    * Alerta de atraso
        * Verifica todos os jogos com status alugado onde a data atual é superior a data de devolução 
            * Nome cliente
            * Telefone do cliente
            * E-mail do cliente
            * Data do pedido
            * Nome do Jogo 
            * Data de Devolução
            * Dias de atraso
    
    * Renovar Pedido
        Muda o status dos jogos de um pedido para Renovado e então cria um novo pedido com os mesmos jogos.
        * Parametros obrigatórios
            * Id do Pedido
        * Parametros opcionais
            * Lista de jogos e faixa de preço
                * Id do jogo
                * Data de Devolução
                * Valor
            Observação: A lista de jogos informada deve ser um subconjunto da lista de jogos do pedido original, ou seja, não é permito adicionar novos jogos em uma renovação. Se informada, apenas os jogos dessa lista estarão no novo pedido, os demais jogos deverão ser devolvidos. Se não for informada, todos os jogos do pedido original serão renovados.

                
            
               

           

# Padrões a serem adotados:  
## Tecnologias utilizadas
- .Net8 
- Asp.Net Core
- Banco de Dados MySQL
- Docker

## Nomenclaturas
Nome de classes: PascalCase. Ex.: JogoTabuleiro, Cliente
Nome de métodos: PascalCase. Ex.: jogo.Alugar()
Nome de variavies locais: camelCase. Ex.: var jogo = new JogoTabuleiro();
Nome de fields privados: _camelCasel. Ex.: _cliente
Nome de constantes: UPPER_SNACK_CASE. Ex.: CATEGORIA_LEVEL_1

# Regras gerais e padrões de código
- Ao declarar namespaces use a abordagem mais moderna onde não é necessário o uso de chaves. Ex. namespace ProximoTurnoApi.Models;
- Nao quebre linha para adicionar chaves, coloque-as logo após a declaração da instrução. Ex.: `If (debug){`
- Para manter o codigo mais simples, ao criar repositórios mantenha a classe e a interface em um mesmo arquivo.
- Todas as tabelas criadas devem conter uma chave primária de nome ID.
- Use funções assincronas nos repositórios.
- Crie migrations para todo o banco de dados.