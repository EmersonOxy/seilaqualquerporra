# Controlador FPS

Jogo e editor de mapas para Windows 10 e 11 (64 bits), feitos em C# com raylib.

## Instalação

Baixe e extraia o pacote geral. Ele contém as pastas `Jogo` e `Editor`. Execute `Jogo\Setup.exe` ou `Editor\Setup.exe`.

- [Pacote geral: jogo e editor](https://github.com/EmersonOxy/seilaqualquerporra/releases/latest/download/ControladorFPS-PacoteGeral.zip)

Na primeira execução, o Setup baixa a versão atual do GitHub e instala o aplicativo em `Aplicativo`, uma subpasta separada. Ao abrir o mesmo Setup novamente, **Sim** atualiza pela última versão do GitHub, **Não** restaura usando o último instalador salvo e **Cancelar** sai. O aplicativo não precisa estar aberto para atualizar.

Os dois aplicativos compartilham o mapa em `Documentos\ControladorFPS\Dados\map.json`. Salve pelo editor; o jogo detecta as mudanças e carrega o mapa sem reinstalação. A pasta de dados é preservada durante as atualizações. O jogo continua funcionando offline.

## Versão

A linha organizada está na versão **1.0.4**. Ela inclui dissolve e partículas ao coletar orbs, água e grama colocáveis pelo editor, e materiais de vidro e holográfico para os objetos. As tags históricas das versões anteriores do jogo foram preservadas. A release atual é marcada explicitamente para que os setups baixem esta linha. Os pacotes de build ficam fora do repositório, em `dist/`, e os dados dos usuários ficam em Documentos.

## Estrutura

- `src/ControladorFPS`: jogo e subsistemas compartilhados.
- `src/MapEditor`: editor de mapas.
- `src/SetupManager`: setups leves que instalam, atualizam e restauram os aplicativos.
- `assets`: áudio e demais recursos compartilhados.
- `tests`: validações do projeto.
- `.github/workflows`: CI e publicação de releases.

Para publicar uma versão, envie uma tag `vX.Y.Z`. A automação compila o jogo e o editor, publica os canais de atualização e gera um único ZIP geral.
