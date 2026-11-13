# Artillery MOBA 2D — versão jogável 0.1

Primeira arena offline do projeto principal, em Unity 6. A versão de reprodução é **6000.0.62f1**. A implementação parte da `main` e reaproveita seletivamente terreno, física, balística e testes da [PoC Unity d88adeb](https://github.com/contatomatheusneves-max/artillery-moba-2d/tree/d88adebc9a268c53c7f64f1afcc025e3c6a3cc37/pocs/unity).

## Abrir no Windows

1. Instale ou selecione [Unity 6000.0.62f1](https://unity.com/releases/editor/whats-new/6000.0.62f1) no Unity Hub.
2. Obtenha a branch e adicione a pasta `game/unity` pelo Unity Hub. Use um Editor ativado. O projeto fixa o Unity Test Framework 1.6.0 fornecido nessa revisão do Editor.
3. Abra `Assets/Scenes/Arena.unity`, clique em **Play** e dê foco à janela **Game**. O controlador da cena cria a arena e a câmera fixa ao iniciar.
4. Se o Console indicar que o Input Manager está desativado, em **Edit → Project Settings → Player → Other Settings → Active Input Handling** selecione **Input Manager (Old)** ou **Both** e reinicie o Editor.

```powershell
git clone --branch feature/m2-01-unity-playable https://github.com/contatomatheusneves-max/artillery-moba-2d.git
```

| Controle | Ação |
| --- | --- |
| **A / D** | Mover o tanque turquesa quando apoiado |
| **↑ / ↓** | Ajustar a mira global de 0° a 180° |
| **Q / E** | Ajustar a potência de 0 a 100 |
| **Espaço** | Disparar a partir da boca do canhão com potência acima de zero |
| **Botão direito ×2** | Escavar um túnel entre dois pontos. Clique duas vezes no mesmo ponto para abrir uma cavidade. **Esc** cancela a marcação |
| **B / R** | Entrar no cenário de quatro disparos no passo 45 ou reiniciar no campo livre |
| **V / Tab / N** | Alternar velocidade para 0,25×, pausar ou avançar um passo enquanto pausado |
| **L / P** | Salvar ou reproduzir o último replay. O Console mostra o caminho do JSON |
| **F2** | Alternar os presets visuais Compacto, Padrão e Alto |

Para uma verificação rápida, use **B** e **V** e acompanhe o pico de quatro tiros e as revisões do terreno. Salve com **L**, reproduza com **P** e confira o resultado do replay no HUD. Em **R**, dirija, atire contra a encosta e escave sob o tanque para observar a perda de apoio e a queda.

## Organização e integração

| Frente | Propriedade e entrega |
| --- | --- |
| Física e terreno | `Artillery.Core`, `Artillery.Runtime`. Grade com cavidades, cache por revisão de bloco, hash incremental, colisores, tanques e ordenação temporal de impactos. |
| Gameplay | `Artillery.Gameplay`. `ArenaSession` transforma intenções em `TickCommand` e gerencia pausa, cenários e replay. A gravação compara hashes e impactos e informa a primeira divergência. |
| Visual e interface | `Artillery.Presentation`, `Assets/Resources`. Cenário em camadas, tanques, HUD, efeitos cosméticos e presets. O visual recebe apenas `IArenaViewState` e `PresentationStatus`. |
| Coordenação | `Artillery.Bootstrap`, `Assets/Scenes` e configurações. O controlador fornece teclado e mouse à sessão, mantém a câmera fixa e apresenta o estado. O teste de cena exercita esse caminho. |

As frentes foram desenvolvidas por três subagentes em cópias de trabalho isoladas. A coordenação definiu os contratos, integrou os arquivos em um projeto e verificou as referências. O projeto conserva a direção de disparo global, sem vento físico, sem colisão entre projéteis e sem HP. Nenhuma ação visual altera o terreno ou as regras.

## Parâmetros e replay

A arena usa 40 × 18 unidades, grade de 160 × 72 células de 0,25 unidade, blocos de 16 × 16, base protegida de 0,50 unidade e paredes físicas para os tanques. A simulação usa passos de 1/60 s, velocidade máxima experimental de 29 unidades/s e até quatro projéteis. O controlador usa `FixedUpdate` para os controles mantidos e enfileira ações discretas de teclado e mouse para o próximo passo. O log reproduz os comandos por passo.

O JSON usa a revisão `m2-01.1` e é gravado em `Application.persistentDataPath/artillery-v01-replay.json`. Abra um replay gravado nesta versão para comparar os hashes por passo e o registro de impactos. O hash cobre o estado observável. Ele não inclui todo o estado interno do solver nativo e não estabelece determinismo entre builds ou plataformas.

## Testes

Na pasta `game/unity`, execute a suíte do núcleo com .NET SDK 10.

```powershell
dotnet run --project .\Tools\CoreChecks\CoreChecks.csproj --configuration Release
```

No Unity, use **Window → General → Test Runner → PlayMode → Run All**. Para executar em lote, feche outras instâncias do mesmo projeto e ajuste o caminho do Editor.

```powershell
$m2Editor = 'C:\Program Files\Unity\Hub\Editor\6000.0.62f1\Editor\Unity.exe'
$m2Project = (Get-Location).Path
New-Item -ItemType Directory -Force .\TestResults | Out-Null
& $m2Editor -batchmode -nographics -projectPath "$m2Project" -runTests -testPlatform PlayMode -testResults "$m2Project\TestResults\playmode.xml" -logFile "$m2Project\TestResults\unity.log"
```

A suíte inclui oito cenários físicos herdados, três testes de sessão e replay e um teste de integração que abre a cena principal, aguarda a salva e reinicia o campo. Para revisão visual, rode a cena com gráficos e percorra os presets com **F2**.

| Verificação desta entrega | Resultado |
| --- | --- |
| Núcleo C# em Windows, .NET SDK 10.0.301, Release | **10/10 passaram**, incluindo cache por bloco, base protegida, impactos simultâneos, reconsulta e replay do núcleo |
| Compilação C# 9 com bibliotecas Unity 6000.0.62f1 | **6 assemblies do projeto compilaram**, incluindo PlayMode |
| Estrutura Unity | **48 GUIDs** conferidos, cena de build, dependências e referências locais verificadas |
| Editor e PlayMode | A execução em lote encontrou falhas de comunicação com o Unity Licensing Client e ficou sem XML de resultados. **12 casos pendentes**, além da inspeção visual e da compilação de shaders no Editor |

## Desempenho, pipeline e pendências

- O cache por bloco reduz reconstrução e cópia de retângulos e evita reprocessar toda a ocupação a cada hash. `CompositeCollider2D.GenerateGeometry()` ainda recompõe o colisor global após cortes efetivos. No Profiler, filtre `Artillery.Simulation.Step`, `Artillery.Terrain.UpdateSources` e `Artillery.Terrain.GenerateGeometry`. Compare a mesma sequência de cortes, sem Deep Profile, e registre mediana, p95 e máximo. Separe a montagem inicial da arena dos cortes em jogo.
- O teste de tiro rápido usa a pose final do tanque naquele passo. Cruzamentos entre poses exigem validação adicional. Os blocos de terreno continuam discretos e o movimento usa contato físico nativo.
- Esta versão conserva o Built-in Render Pipeline para limitar mudanças no projeto validado. A URP tem um [Renderer 2D](https://docs.unity3d.com/6000.0/Documentation/Manual/urp/Setup.html) com iluminação 2D. Uma avaliação futura deve verificar shaders e materiais, custo de luzes e transparência e comportamento em GPU de desktop e mobile. Os três presets já controlam apenas a apresentação. Entrada por toque, builds mobile e a meta experimental de 1080p/60 FPS precisam de medição em dispositivos.
