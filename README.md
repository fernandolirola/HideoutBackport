# HideoutBackport

Brings the hideout from the current live game version (1.2.0.0.47965) to SPT 4.1.6.

- **Upgrade requirements**: items, amounts, trader levels, skills and other areas, as on live.
- **Found in raid**: live dropped the found-in-raid rule from about 200 upgrade requirements. Keep it or turn it off in the config.
- **Construction times** and **upgrade bonuses** from live.
- **Crafts**: live production times, ingredients and outputs, new crafts and retired crafts.
  New crafts are only added when every item they use exists on your server (other mods such as
  content backports can provide them). Crafts unlocked by quests that this version does not have are skipped.
  Crafts that live opened to everyone (Virtex, VOG-17, UHF RFID Reader) no longer need their quest.
- Server only. Your hideout progress is kept; the new values apply to the next upgrades and crafts.

## Compatibility

- Runs at the start of loading, before mods that tweak the hideout. **SVM** hideout options
  (time multipliers, removed requirements, found-in-raid removal) apply on top of the live data. Tested with SVM 2.2.3.
- Mods that add hideout crafts of their own keep them.

## Install

Extract the zip into your game folder (the one that contains `SPT_Runtime`), so you end up with
`SPT_Runtime/user/mods/HideoutBackport`.

## Config (`config.json`)

| Option | Default | What it does |
|---|---|---|
| `Enabled` | true | Turns the mod on or off |
| `UpgradeRequirements` | true | Live upgrade requirements |
| `LiveFoundInRaidRules` | true | Live found-in-raid rules. `false` keeps the old rule for items that were already required |
| `ConstructionTimes` | true | Live construction times |
| `UpgradeBonuses` | true | Live upgrade bonuses |
| `CraftTimes` | true | Live production times |
| `CraftRecipes` | true | Live ingredients and outputs |
| `NewCrafts` | true | Adds crafts that only exist on live |
| `RemoveRetiredCrafts` | true | Removes crafts that live no longer has |
| `ScavCaseTimes` | true | Live scav case times |
| `Debug` | false | Logs every skipped level or craft |

## Data and credits

The hideout data in `data/` comes from the SPT server project
(<https://github.com/SP-Tushonka/server-csharp>, branch `5.0x-dev`, commit `fbefff2`), licensed under
**CC BY-NC-SA 4.0** (see `data/LICENSE-DATA.md`). The mod code is MIT.

---

## Português

Traz o hideout da versão live atual do jogo (1.2.0.0.47965) para o SPT 4.1.6.

- **Requisitos de upgrade**: itens, quantidades, níveis de comerciante, skills e outras áreas, como no live.
- **Encontrado em raid (FIR)**: o live tirou a exigência de FIR de cerca de 200 requisitos. Dá para manter ou desligar no config.
- **Tempos de construção** e **bônus dos upgrades** do live.
- **Crafts**: tempos, ingredientes e produtos do live, crafts novos e crafts removidos.
  Crafts novos só entram quando todos os itens existem no seu servidor (mods de backport de conteúdo podem trazê-los).
  Crafts liberados por missões que esta versão não tem são pulados.
  Crafts que o live liberou para todos (Virtex, VOG-17, leitor UHF RFID) não precisam mais da missão.
- Só servidor. Seu progresso no hideout é mantido; os valores novos valem para os próximos upgrades e crafts.

### Compatibilidade

- Roda no começo do carregamento, antes dos mods que ajustam o hideout. As opções de hideout do **SVM**
  (multiplicadores de tempo, remover requisitos, remover FIR) são aplicadas por cima dos dados live. Testado com o SVM 2.2.3.
- Crafts adicionados por outros mods são mantidos.

### Instalação

Extraia o zip na pasta do jogo (a que contém `SPT_Runtime`), ficando `SPT_Runtime/user/mods/HideoutBackport`.

### Dados e créditos

Os dados de hideout em `data/` vêm do projeto do servidor do SPT (branch `5.0x-dev`, commit `fbefff2`),
sob licença **CC BY-NC-SA 4.0**. O código do mod é MIT.

<sub>Enjoying the mod? Curtiu o mod? [Buy me a coffee on Ko-fi](https://ko-fi.com/zezinhoeft)</sub>
