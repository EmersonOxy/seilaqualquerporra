# Controlador FPS

Jogo e editor de mapas para Windows 10 e 11 (64 bits), feitos em C# com raylib.

## Instalação

Baixe e extraia o pacote geral. Ele contém as pastas `Jogo` e `Editor`. Execute `Jogo\Setup.exe` ou `Editor\Setup.exe`.

- [Pacote geral: jogo e editor](https://github.com/EmersonOxy/seilaqualquerporra/releases/latest/download/ControladorFPS-PacoteGeral.zip)

Na primeira execução, o Setup baixa a versão atual do GitHub e instala o aplicativo em `Aplicativo`, uma subpasta separada. Ao abrir o mesmo Setup novamente, **Sim** atualiza pela última versão do GitHub, **Não** restaura usando o último instalador salvo e **Cancelar** sai. O aplicativo não precisa estar aberto para atualizar.

Os dois aplicativos compartilham o mapa em `Documentos\ControladorFPS\Dados\map.json`. Salve pelo editor; o jogo detecta as mudanças e carrega o mapa sem reinstalação. A pasta de dados é preservada durante as atualizações. O jogo continua funcionando offline.

## Publicar uma versão

Crie e envie uma tag `vX.Y.Z` para o GitHub. A automação compila os aplicativos, atualiza os canais do jogo/editor e publica um único ZIP com os dois setups de gerenciamento.
