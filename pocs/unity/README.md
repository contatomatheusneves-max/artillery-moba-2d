# G2-01 — Artilharia 2D na Unity 6

PoC local para avaliar terreno destrutível, contato dos tanques, quatro disparos no mesmo passo e reprodução de partidas curtas. Base histórica M0: `9edd28d49e690daff9e80d512e6d0ea228f49a40`.

## Abrir no Windows

1. Obtenha a branch `poc/g2-01-unity-terrain`.

   ```powershell
   git clone --branch poc/g2-01-unity-terrain https://github.com/contatomatheusneves-max/artillery-moba-2d.git
   ```

2. No Unity Hub, instale ou selecione o [Unity Editor 6000.0.62f1](https://unity.com/releases/editor/whats-new/6000.0.62f1). Para repetir os testes com o mesmo ambiente, use essa revisão. A primeira abertura resolve o Unity Test Framework pelo registro da Unity.
3. No Hub, escolha **Add → Add project from disk** e selecione a pasta `artillery-moba-2d/pocs/unity`.
4. Na janela **Project**, abra `Assets/Scenes/G2-01.unity`. Clique em **Play** e dê foco à aba **Game**. O cenário e o HUD são montados em Play Mode.
5. Se o Console indicar que o Input Manager está desativado, abra **Edit → Project Settings → Player → Other Settings → Active Input Handling**, selecione **Input Manager (Old)** ou **Both** e reinicie o Editor quando solicitado.

| Controle | Ação |
| --- | --- |
| **A / D** | Dirigir o tanque turquesa enquanto o casco toca o terreno |
| **↑ / ↓** | Ajustar a mira global. 0° aponta à direita, 90° para cima e 180° à esquerda |
| **Q / E** | Reduzir ou aumentar a potência de 0 a 100 |
| **Espaço** | Disparar com potência acima de zero |
| **Botão direito duas vezes** | Escavar entre dois pontos. Clique duas vezes no mesmo ponto para abrir uma cavidade. **Esc** cancela a primeira marcação |
| **B** | Abrir a arena de salva. Quatro tanques lançam um projétil cada no passo 45, com ângulo de 7° e potência 100 |
| **R** | Reiniciar no campo de testes |
| **V**, **Tab**, **N** | Alternar velocidade para 0,25×, pausar e avançar um passo enquanto pausado |
| **L / P** | Gravar e reproduzir o último replay local. O Console mostra o caminho do JSON ao gravar |

## Roteiro de verificação no Editor

1. **Cratera e túnel.** No campo inicial, dispare contra a encosta central. Confira o recorte e a revisão do terreno no HUD. Marque dois pontos na parte maciça da colina, abaixo da superfície, e confirme que há espaço vazio com um teto intacto. Escave sob o tanque e observe a queda até o próximo apoio.
2. **Mira e tanques.** Mantenha **Q** até potência 0 e pressione **Espaço**. Aumente a potência e confira visualmente 0°, 90° e 180°. Atire e observe o início do voo a partir da boca do canhão. Depois de **B**, aguarde a salva terminar, aponte a 180° e dispare para a esquerda contra o tanque seguinte.
3. **Quatro projéteis.** Ative 0,25× com **V**, pressione **B** e deixe os tanques parados. O HUD deve registrar pico de 4. Use **Tab/N** perto do passo 45 para inspecionar o mesmo passo de lançamento e a sequência de impactos no muro. A revisão deve avançar com a deformação e os impactos seguintes podem avançar para dentro da parede.
4. **Replay.** Após a salva, pressione **L** e depois **P**. Espere a reprodução terminar. O HUD informa se todos os hashes coincidiram ou em qual passo apareceu a primeira divergência. Guarde o JSON e o log do Editor se houver divergência.

## Testes

| Verificação | Resultado nesta entrega |
| --- | --- |
| C# puro em Windows, SDK .NET 10.0.301, Release | **9/9 passaram** |
| Cratera, túnel com teto, cobertura exata da malha lógica e revisões nos limites dos blocos | Passou no núcleo C# |
| Base protegida contra cratera e túnel atravessando toda a largura | Passou no núcleo C#. Os cortes removem células acima da base e um corte restrito à base preserva a revisão do terreno |
| Mira, potência zero, gravidade e quatro projéteis coincidentes sem colisão entre eles | Passou no núcleo C# |
| Impactos distintos consultam a geometria atualizada no mesmo passo e empates entram no mesmo grupo | Passou com consulta controlada no núcleo C# |
| Replay de 180 passos com as mesmas entradas, em cadências de 30 e 144 chamadas por segundo | Os hashes do núcleo coincidiram |
| Salva contra a grade com consulta amostrada | Impactos em X ≈ 17,01, 18,75, 20,51 e 31,95 unidades. Evidência do núcleo e do cenário de teste |
| Sintaxe C# 9 e metadados de assets | 12 arquivos C# sem erros sintáticos e 32 GUIDs únicos |
| Oito testes PlayMode, importação e verificação visual | Pendentes. O Unity Editor estava indisponível neste ambiente |

| Testes PlayMode | Cobertura |
| --- | --- |
| Cratera e túnel | Raycast e sobreposição conferem cratera, passagem livre, teto e piso sólidos |
| Apoio e movimento do tanque | Movimento no chão e queda com novo apoio após escavação |
| Salva | Quatro projéteis coexistem e um impacto posterior usa a revisão nova do terreno |
| Tiro contra tanque parado | Potência zero impede lançamento. Tiro a 180° acerta outro tanque e registra o impacto |
| Replay | Compara impactos e 180 hashes de duas cenas físicas novas. A segunda aplica os comandos gravados pela primeira em uma cadência diferente |
| Base indestrutível | Cratera e escavação profunda deixam o colisor inferior intacto. O tanque cai e se apoia nele |
| Limites laterais | O tanque é dirigido contra cada parede. O teste verifica a contenção do casco a cada passo e a desaceleração horizontal |
| Projétil rápido e alvo móvel | Disparo a 29 unidades/s contra um tanque em queda e deslocamento lateral. O teste verifica o alvo e a velocidade dele no passo do impacto |

Para executar os testes independentes do Editor, abra PowerShell em `pocs/unity` com o .NET SDK 10 instalado.

```powershell
dotnet run --project .\Tools\CoreChecks\CoreChecks.csproj --configuration Release
```

Para executar os oito testes com colisores e corpos nativos, use **Window → General → Test Runner → PlayMode → Run All**. Em um Windows com o Editor ativado, também é possível rodar sem gráficos. Feche outras instâncias do Editor com este projeto aberto e execute em `pocs/unity`.

```powershell
$g2Editor = 'C:\Program Files\Unity\Hub\Editor\6000.0.62f1\Editor\Unity.exe'
$g2Project = (Get-Location).Path
New-Item -ItemType Directory -Force .\TestResults | Out-Null
& $g2Editor -batchmode -nographics -projectPath "$g2Project" -runTests -testPlatform PlayMode -testResults "$g2Project\TestResults\playmode.xml" -logFile "$g2Project\TestResults\unity.log"
```

Registre a revisão da Unity, o Windows, o commit e se o teste rodou com gráficos ou em `-nographics`. Guarde o XML e o log. Se o replay divergir, guarde também o JSON e o primeiro passo divergente.

## Medir a regeneração de colisores

1. Abra **Window → Analysis → Profiler**. Use **CPU Usage** com **Deep Profile** desativado. Registre versão do Editor, hardware e se o alvo é Editor ou Development Build.
2. No campo inicial, faça uma sequência de pelo menos 12 cortes em regiões sólidas. Aguarde a revisão do terreno mudar entre cortes e inclua cortes que cruzem limites de blocos. Pressione **L** e guarde o JSON indicado no Console. Reproduza com **P** uma vez para aquecimento e capture as próximas reproduções. Para uma segunda carga, grave uma execução separada da salva com **B**.
3. Na **Timeline** da thread principal, localize `G2.Terrain.GenerateGeometry`. O marcador mede apenas a chamada `CompositeCollider2D.GenerateGeometry()`. Desconsidere a primeira amostra de cada replay, que corresponde à montagem inicial do cenário. Cortes que deixam a grade igual ficam sem amostra de regeneração.
4. Para cada carga, guarde a captura e o replay. Registre quantidade de chamadas, mediana, p95 e máximo em ms por chamada. Se um quadro contiver várias chamadas, registre também o total do marcador no quadro. Compare Editor e Development Build em registros separados.

## Arquitetura e decisões experimentais

| Caminho | Responsabilidade |
| --- | --- |
| `Assets/G2/Core/Terrain` | Grade sólida bidimensional, cortes circulares e em cápsula, revisão de terreno e decomposição em retângulos |
| `Assets/G2/Core/Projectile` | Integração balística, capacidade de quatro disparos e ordenação temporal dos impactos |
| `Assets/G2/Core/Simulation` | Relógio de 60 passos/s, cenários e hash de estado |
| `Assets/G2/Unity/Terrain` | Polígonos por bloco e atualização do colisor composto |
| `Assets/G2/Unity/Physics` e `Tank` | Consultas circulares, paredes laterais estáticas e corpos rígidos dos tanques |
| `Assets/G2/Unity/Simulation` | Comandos por passo, mutações de terreno e registro de replay |
| `Assets/G2/Unity/Presentation`, `Assets/Scenes` e `Assets/Resources` | Cena, controles, câmera fixa, malha, canhão e HUD |
| `Assets/Tests/PlayMode` e `Tools/CoreChecks` | Testes de integração nativa e testes do núcleo C# |

A grade de 160 × 72 células tem resolução de 0,25 unidade e blocos de 16 × 16 células. Nos dois cenários, as duas fileiras inferiores (0,50 unidade) formam uma base sólida protegida de crateras e túneis. A espessura é experimental. Cada bloco alterado fornece retângulos que alimentam a malha visual e os polígonos de colisão. Um único `CompositeCollider2D` estático une os polígonos de todos os blocos. Ele é regenerado após cada grupo de cortes antes de uma nova consulta de projétil, inclusive no mesmo passo. A malha visual é atualizada na próxima apresentação. Duas paredes estáticas independentes do terreno contêm os tanques em X = 0 e X = 40, com margem vertical em toda a altura útil da arena. Os projéteis continuam usando os limites de descarte da balística.

`PocWorld.Step` aplica o comando, avança a cena física 2D isolada e consulta as trajetórias contra a geometria atual e a pose final dos tanques daquele passo. O impacto mais cedo remove seus projéteis e modifica o terreno. Os projéteis restantes são consultados novamente a partir daquela fração do passo. Impactos com diferença de até 0,00001 da fração de um passo são agrupados e seus cortes destrutivos formam uma união. A PoC só implementa remoção de terreno. As colisões com tanques registram impacto, sem dano ou eliminação. As consultas ignoram o tanque que disparou e os projéteis não têm colisores uns contra os outros. O canhão usa a direção global mesmo se o casco inclinar.

A gravidade, o tamanho das células, os raios, as velocidades, o limite de quatro projéteis ativos e a exclusão do tanque de origem são parâmetros da prova. A câmera é fixa e a HUD mostra estado observado. A entrada do teclado é amostrada na renderização e convertida em comandos por passo. Comandos gravados são reproduzidos no mesmo passo, independentemente da cadência de renderização. O acumulador limita a oito passos por quadro e carrega o excedente. Pausar descarta o tempo parcial pendente.

## Limitações e servidor autoritativo

- A resolução gera degraus na superfície e pode afetar o contato dos tanques. A grade e a malha localizam o trabalho por bloco, mas o colisor composto ainda regenera o conjunto. Terreno muito recortado eleva o número de caminhos e o custo de regeneração. O hash percorre toda a grade por passo e a gravação cresce com a duração da execução. Não houve medição de tempo, memória ou GPU.
- A varredura circular reduz o risco de atravessar terreno entre passos. Para tanques, ela usa a pose final do casco em cada passo. O caso de alvo móvel cobre uma interseção esperada nessa aproximação. Um casco que cruze a trajetória inteiramente entre amostras ainda pode escapar da detecção. Se esse caso for relevante, avalie subpassos sincronizados ou varredura relativa com as poses do casco no início e no fim do passo.
- O JSON registra versão da PoC, Unity, plataforma, cenário, comandos por passo, impactos com revisões do terreno e hashes do estado observável. Os hashes incluem grade, projéteis e pose/velocidade dos tanques. O estado interno do solver 2D não faz parte do hash. A revisão do replay é `g2-01.2`; grave novamente os replays desta PoC. Compatibilidade entre plataformas, revisões da Unity e builds continua em aberto.
- `PocWorld` pode ser instanciado sem câmera, HUD, shader ou malha, como nos testes PlayMode. Essa é a base para testar um servidor Unity sem renderização e emitir estados e revisões de terreno. Um servidor .NET independente reutilizaria a grade, a balística e o relógio, mas precisaria de outro backend para consultas e movimento dos corpos. A compilação de servidor e a execução da física nativa sem gráficos ainda precisam de validação.

Referências oficiais: [PhysicsScene2D.Simulate](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/PhysicsScene2D.Simulate.html), [CompositeCollider2D](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/CompositeCollider2D.html), [ProfilerMarker.Auto](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Unity.Profiling.ProfilerMarker.Auto.html) e [Unity Test Framework CLI](https://docs.unity3d.com/Packages/com.unity.test-framework@1.4/manual/reference-command-line.html).
