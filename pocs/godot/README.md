# G2-02 — Artilharia 2D na Godot 4

Prova de conceito independente, em **Godot 4.7.2 Stable, edição padrão com GDScript**. Referência funcional: [Unity G2-01 no commit d88adeb](https://github.com/contatomatheusneves-max/artillery-moba-2d/tree/d88adebc9a268c53c7f64f1afcc025e3c6a3cc37/pocs/unity). Base: `main` no commit histórico `9edd28d49e690daff9e80d512e6d0ea228f49a40`. Parâmetros experimentais conforme o GDD v6.15, §16.33.

## Abrir no Windows

1. Baixe a [Godot 4.7.2 Stable, Windows Standard](https://godotengine.org/download/archive/4.7.2-stable/) e extraia o ZIP.
2. Obtenha a branch da PoC e abra a pasta do projeto.

   ```powershell
   git clone --branch poc/g2-02-godot-terrain https://github.com/contatomatheusneves-max/artillery-moba-2d.git
   Set-Location .\artillery-moba-2d\pocs\godot
   ```

3. No gerenciador da Godot, use **Import** e selecione `pocs/godot/project.godot`. Abra o projeto e pressione **F5** para rodar `presentation/arena.tscn`.

| Controle | Ação |
| --- | --- |
| **A / D** | Dirigir o tanque turquesa |
| **↑ / ↓**, **Q / E** | Ajustar mira global de 0° a 180° e potência de 0 a 100 |
| **Espaço** | Disparar com potência acima de zero |
| **Botão direito ×2** | Escavar entre dois pontos. Use o mesmo ponto para uma cavidade circular. **Esc** cancela a primeira marcação |
| **B**, **R** | Carregar a salva de quatro tiros no passo 45 ou voltar ao campo livre |
| **V**, **Tab**, **N** | Alternar para 0,25×, pausar ou avançar um passo enquanto pausado |
| **L**, **P** | Salvar ou reproduzir o último replay JSON, com comandos, hashes e impactos |
| **M** | Salvar o relatório JSON de métricas da execução atual |

Os caminhos do replay e das métricas aparecem no console. Para inspeção manual, use **B**, diminua a velocidade com **V** e observe o pico de quatro projéteis e as revisões do terreno. Depois salve e reproduza com **L/P**. O HUD indica o primeiro passo divergente ou a coincidência de hashes e impactos.

## Executar testes

Na pasta `pocs/godot`, ajuste o caminho para o executável console extraído da distribuição padrão.

```powershell
$g2Godot = 'C:\Godot\Godot_v4.7.2-stable_win64_console.exe'
& $g2Godot --version
New-Item -ItemType Directory -Force .\logs | Out-Null
& $g2Godot --headless --path (Get-Location).Path --script res://tests/run.gd --log-file logs/g2-02-tests.log
$LASTEXITCODE
```

O executor nativo `SceneTree` imprime um resultado por teste, métricas da carga de replay e retorna 0 quando todos passam ou 1 se a suíte detectar falha. Também é possível abrir `tests/editor_run.tscn` e pressionar **F6** para executar a mesma suíte no Editor. Use o log e a versão exibida para registrar a evidência.

| Teste | Cobertura |
| --- | --- |
| 1. Cratera e túnel | Superfície rebaixada, passagem livre, teto e piso preservados após atualização das colisões |
| 2. Tanque e apoio | Movimento sobre o solo e queda até novo apoio depois de escavar |
| 3. Salva | Quatro tiros no mesmo passo, impacto posterior mais profundo, reconsulta no mesmo passo e união com ordem invertida |
| 4. Potência e alvo parado | Potência zero, mira cardinal e impacto no tanque vizinho |
| 5. Replay | Serialização JSON, 180 passos em cadências sintéticas de 30 e 144 chamadas/s, comparação de hashes e impactos |
| 6. Fundo | Camada de 0,50 unidade após corte circular e túnel, suporte do tanque e corte protegido sem nova revisão |
| 7. Laterais | Casco contido em ambas as bordas durante movimento para fora |
| 8. Alvo móvel | Tiro a 29 unidades/s contra tanque em queda e cruzamento sintético inteiramente entre duas poses |

**Evidência desta entrega.** A análise gramatical externa passou em **16/16 scripts GDScript**. Os oito testes da engine, a importação nativa e a inspeção visual permanecem pendentes. O executável Windows não estava disponível e as duas rotas de download foram bloqueadas. O Editor web oficial abriu na versão 4.7.2, mas a importação do projeto ficou bloqueada pelo acesso a arquivos locais. Nenhuma medição de runtime foi coletada. Entre a primeira escrita do projeto e a análise sintática decorreram aproximadamente 14 minutos, incluindo tentativas de acesso ao runtime. Esse intervalo não mede horas de engenharia comparáveis.

## Representação e paridade

| Aspecto | Godot G2-02 | Referência Unity G2-01 |
| --- | --- | --- |
| Arena e cenários | 40 × 18, grade 160 × 72, células 0,25, blocos 16 × 16. Campo livre com cavidade e parede de salva. Base de 0,50 e paredes fixas | Mesmas dimensões, mapas e parâmetros experimentais |
| Terreno | Grade de ocupação com retângulos sólidos por bloco. Cada corte atualiza apenas os blocos alterados. Colisão e desenho consultam esses retângulos | Grade e polígonos por bloco reunidos em `CompositeCollider2D` |
| Tanque | Casco retangular cinemático, gravidade, contato, degraus e limites sólidos | Cápsula com corpo rígido e rotação do casco |
| Projétil | Círculo varrido sobre o terreno. Para tanques, varredura relativa à translação linear durante o passo | Varredura circular contra a pose do tanque no fim do passo |
| Simulação e replay | 60 Hz, quatro projéteis, velocidade máxima de 29 unidades/s, comandos e impactos em JSON, SHA-256 do estado observável | Mesmos parâmetros de comparação e verificação própria dos hashes |

`terrain/grid.gd` mantém a ocupação e o cache de colisão. `physics/` concentra varreduras e tanques, `projectile/` concentra lançamento e ordem dos eventos, `core/simulation.gd` coordena os passos, `replay/` define o registro e `presentation/` contém câmera fixa, desenho, HUD e entrada. A cena lê o estado da simulação sem participar da resolução de colisões.

Os impactos mais próximos no tempo são resolvidos antes dos demais. Os que caem na mesma tolerância temporal formam uma única união destrutiva, independente da ordem das escavações. A geometria dos blocos afetados é reconstruída antes da próxima varredura. Os projéteis atravessam uns aos outros e ignoram o tanque de origem. Paredes laterais contêm os tanques, enquanto projéteis seguem seus limites de descarte. A PoC implementa remoção de material e registra impactos sem HP.

## Medir antes de comparar

1. Registre commit, versão da engine, CPU, GPU, Windows, build, resolução e VSync. Use a resolução de janela `1920x1080` para a meta experimental, por exemplo com `--resolution 1920x1080`. Confirme o tamanho efetivo no JSON.
2. Grave uma execução da salva e outra com cortes sucessivos usando **L**. Guarde os arquivos separadamente. Reproduza cada carga uma vez para aquecer e depois repita com as mesmas condições. Use **M** ao terminar cada coleta.
3. Compare mediana, p95 e máximo de `collider_us`, `step_us` e `frame_us`, o número de revisões e o pico de blocos sujos. `collider_us` mede apenas a reconstrução dos retângulos de colisão afetados. `step_us` inclui a simulação e o hash. `frame_us` mede o intervalo de parede entre chamadas de apresentação. A janela de métricas retém até 12.000 amostras por série.
4. Registre memória estática da engine quando disponível. Meça memória total do processo e VRAM separadamente. O relatório headless da suíte descreve a carga de replay e omite frame gráfico. Compare dados entre engines depois de repetir as mesmas cargas no mesmo hardware.

## Limitações e servidor autoritativo

- A grade produz degraus de 0,25 unidade. Terrenos muito fragmentados aumentam a quantidade de retângulos e alocações de consulta. O hash da grade é recalculado quando ela muda e o replay cresce com a quantidade de passos.
- O casco permanece alinhado aos eixos e o movimento dos tanques é resolvido em ordem estável, eixo por eixo. A varredura de projétil contra tanque interpola a translação entre poses. Rotação, contatos curvos e impulsos coletivos exigem outro nível de modelagem. A PoC avalia a simulação cinemática em GDScript e a apresentação da Godot. O solver nativo de corpos rígidos fica fora desta medição.
- A lógica do mundo é `RefCounted` e pode ser instanciada sem cena visual. A suíte fornece uma entrada `--headless` para validar essa separação. Um servidor futuro pode ser dono dos passos e dos comandos, com snapshots e revisões de terreno. Repetição entre builds, plataformas e arquiteturas continua em aberto.
- O renderizador Compatibility, a câmera fixa e os blocos sujos reduzem dependências. A carga de GDScript, alocações, memória, taxa de quadros e entrada por toque ainda precisam de avaliação em dispositivos mobile.

Referências: [linha de comando da Godot](https://docs.godotengine.org/en/stable/tutorials/editor/command_line_tutorial.html) e [monitor de desempenho](https://docs.godotengine.org/en/stable/classes/class_performance.html).
