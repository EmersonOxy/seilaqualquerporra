# Controlador FPS

Jogo e editor de mapas para Windows 10 e 11 (64 bits), feitos em C# com raylib.

## Instalação

Baixe e instale cada aplicativo uma vez. Eles ficam em pastas separadas e podem ser atualizados independentemente:

- [Instalador do jogo](https://github.com/EmersonOxy/seilaqualquerporra/releases/latest/download/ControladorFPS-win-Setup.exe)
- [Instalador do editor de mapas](https://github.com/EmersonOxy/seilaqualquerporra/releases/latest/download/MapEditor-editor-Setup.exe)

O jogo e o editor têm atualizadores próprios. Pressione **F6** dentro do aplicativo correspondente para procurar e instalar atualizações. O executável de atualização do Velopack também fica na pasta de cada instalação.

Os dois aplicativos compartilham o mapa em `%LOCALAPPDATA%\ControladorFPS\Dados\map.json`. Salve pelo editor; o jogo detecta as mudanças e carrega o mapa sem reinstalação. A pasta de dados fica fora das pastas dos aplicativos e é preservada durante as atualizações. O jogo continua funcionando offline.

## Publicar uma versão

Crie e envie uma tag `vX.Y.Z` para o GitHub. A automação compila e publica instaladores independentes para o jogo e o editor, cada qual no seu canal de atualização.
