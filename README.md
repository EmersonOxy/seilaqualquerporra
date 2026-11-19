# Controlador FPS

Jogo e editor de mapas para Windows 10 e 11 (64 bits), feitos em C# com raylib.

## Instalação

Baixe os dois pacotes ZIP e extraia cada um na pasta em que quer manter o aplicativo. Execute `Instalar-Jogo.cmd` ou `Instalar-Editor.cmd` dentro da pasta extraída; esses atalhos chamam o executável `Setup` e instalam o aplicativo ao lado dele, sem usar `%LOCALAPPDATA%`. O arquivo `LEIA-ME.txt` dentro de cada pasta explica para que serve cada arquivo.

- [Pasta do jogo](https://github.com/EmersonOxy/seilaqualquerporra/releases/latest/download/ControladorFPS-Jogo.zip)
- [Pasta do editor](https://github.com/EmersonOxy/seilaqualquerporra/releases/latest/download/MapEditor-Editor.zip)

O jogo e o editor ficam em pastas distintas, junto dos seus setups, e têm atualizadores próprios. Para atualizar sem abrir o aplicativo, execute `Atualizar-Jogo.cmd` ou `Atualizar-Editor.cmd` na pasta correspondente; eles usam a instalação ao lado ou, para instalações antigas, a pasta padrão em `%LOCALAPPDATA%`. Também é possível pressionar **F6** dentro do aplicativo. `Update.exe` fica na pasta de cada instalação.

Os dois aplicativos compartilham o mapa em `Documentos\ControladorFPS\Dados\map.json`. Salve pelo editor; o jogo detecta as mudanças e carrega o mapa sem reinstalação. A pasta de dados é preservada durante as atualizações. O jogo continua funcionando offline.

## Publicar uma versão

Crie e envie uma tag `vX.Y.Z` para o GitHub. A automação compila e publica instaladores independentes para o jogo e o editor, cada qual no seu canal de atualização.
