# Инструкция: uml-viewer-dotnet

Интерактивный UML viewer на **.NET 8 + Avalonia**. Читает тот же IR (`.edn` / JSON) и каталог **`.metrics/`**, что и Clojure `uml-viewer`, без JVM.

---

## 1. Что нужно заранее

| Компонент | Назначение |
|-----------|------------|
| **Solution / исходники** | Открытие файлов по клику из карточки класса |
| **`<root>/.metrics/crap.edn`** | CRAP, cyclomatic complexity, coverage (crapper) |
| **`<root>/.metrics/mutate/*.edn`** | Mutation score по namespace и методам (mutator) |
| **IR-файл** | Граф классов и рёбер — `.edn` (рекомендуется) или `.json` |

**`--root`** — каталог solution, в котором лежит `.metrics` (или родитель при обходе вверх от пути к IR). Если `--root` не указан, корень метрик ищется автоматически от файла IR.

Подробнее про C# scan и стыковку `:namespace` с crapper/mutator — в Clojure-репозитории: [csharp-project.ru.md](../../uml-viewer/docs/csharp-project.ru.md).

---

## 2. Сборка и тесты

```powershell
cd D:\path\to\uml-viewer-dotnet
dotnet build
dotnet test
```

Проекты:

| Проект | Назначение |
|--------|------------|
| `UmlViewer.App` | GUI (основной способ работы) |
| `UmlViewer.Cli` | `scan`, `overlay-check`, `view` |
| `UmlViewer.Core` | IR, overlay, mailbox |
| `UmlViewer.Engine` | layout, routing, proposals |
| `UmlViewer.Scan` | Roslyn-скан C# → JSON IR |

---

## 3. Откуда взять IR

### Вариант A — Clojure (максимальная совместимость)

```powershell
cd path\to\uml-viewer
clj -M:ir examples\glpi.policy.edn target\glpi-scan.edn
```

Policy в EDN; viewer понимает hierarchical IR, stacked `:diagrams`, `:proposals` и т.д.

### Вариант B — только .NET (Roslyn scan → JSON)

```powershell
cd uml-viewer-dotnet
dotnet run --project src\UmlViewer.Cli -- scan examples\glpi.policy.json out\glpi.json
```

Policy — JSON или EDN (см. `examples/glpi.policy.json`: `title`, `prefix`, `hierarchical`, `sources` с `:lang` / `lang: csharp` и `root`).

Проверка, что namespace из crap.edn есть в IR:

```powershell
dotnet run --project src\UmlViewer.Cli -- overlay-check out\glpi.json --root "D:\path\to\YourSolution"
```

---

## 4. Запуск GUI

### Командная строка

```powershell
dotnet run --project src\UmlViewer.App -- `
  --root "D:\path\to\TP.Cmdb.Glpi.Sync.Service" `
  --ir "D:\path\to\glpi-scan.edn"
```

| Аргумент | Обязательный | Описание |
|----------|--------------|----------|
| `--ir` | да* | Путь к существующему `.edn` или `.json` |
| `--root` | нет | Корень solution с `.metrics` |
| `--restart` | нет | Восстановить камеру, фокус, proposal и declutter из `.uml-viewer/session.edn` |

\* Если `--ir` не передан, приложение ищет `uml-viewer/target/glpi-scan.edn` относительно текущей папки или рядом с бинарником (удобно для dev, но в проде лучше явный путь).

### Visual Studio / Rider

Отредактируйте `src/UmlViewer.App/Properties/launchSettings.json` — поле `commandLineArgs` с реальными путями (не плейсхолдеры с `...`).

### Через CLI-обёртку

```powershell
dotnet run --project src\UmlViewer.Cli -- view `
  --root "D:\path\to\YourSolution" `
  --ir out\glpi.json
```

Или с генерацией IR из policy:

```powershell
dotnet run --project src\UmlViewer.Cli -- view `
  --root "D:\path\to\YourSolution" `
  --policy examples\glpi.policy.json
```

---

## 5. Интерфейс

### Шапка

- **Declutter** — цикл режимов упрощения диаграммы (как в Clojure: none → arrows → triangles → elements → classes → …). Перекомпилирует сцену.
- **Regen** — ставит в очередь агенту операцию `:regen` (см. раздел 7).
- Строка статуса почты — результат последней команды агенту.

### Канва

| Действие | Эффект |
|----------|--------|
| Клик по классу / пакету | Выбор, инспектор справа |
| **Двойной клик** по пакету (drill) | Внутрь namespace |
| **Двойной клик** по классу | Окно **Class detail** (методы, CRAP/CC/cov, mutation) |
| **←** на канве или **Esc** | Назад из drill / layer view |
| **Стрелки** | Панорама |
| **Ctrl + ЛКМ** или **СКМ + drag** | Панорама |
| **Колёсико** | Масштаб 0.25× … 3× |
| **ПКМ** по элементу | Меню команд агенту (CRAP / mutation / omit) |
| Наведение на ребро / dependency | Всплывающая подсказка |

На классах — точки **C** / **M** (grades) и heat по метрикам, как в Clojure viewer.

### Боковая панель «Diagrams»

- Верхняя кнопка — **реальная** диаграмма (IR как есть); клик шлёт агенту `:context` с `:real`.
- Список **proposals** — альтернативные архитектурные срезы из `:proposals` в IR; клик → `:context` `:proposal`.
- **+ New proposal** — новый proposal (сохраняется в IR и при наличии — в соседний `*.policy.edn`).
- **ПКМ** по proposal — переименовать / удалить.

Для proposal: **двойной клик** по `proposal.*` пакету или вложенной группе открывает **layer view** (слои `:layers`).

### Инспектор

- Текст по выбранному элементу.
- **Class detail…** — то же окно детализации, что и двойной клик по классу; клик по строке метода открывает исходник на нужной строке.
- **Open source file** — файл класса в редакторе ОС.

---

## 6. Автообновление IR и метрик

Фоновый опрос (~400 ms):

1. Если изменился **mtime IR** или **stamp `.metrics`** (crap + mutate) — документ перечитывается, overlay накладывается заново, диаграмма перестраивается.
2. Читается очередь **`.uml-viewer/to-viewer.edn`**:
   - `:display` + `:path` — переключить IR (относительно `--root` или абсолютный путь);
   - `:quit-for-restart` — сохранить session и закрыть окно (для перезапуска viewer агентом).

После regen crapper/mutator и перезаписи IR достаточно сохранить файлы — viewer подхватит без ручного перезапуска.

---

## 7. Агент и companion (как у Clojure)

Каталог: **`<metricsRoot>/.uml-viewer/`**

| Файл | Направление |
|------|-------------|
| `to-agent.edn` | Viewer → агент (очередь команд) |
| `to-viewer.edn` | Агент → viewer |
| `session.edn` | Камера, фокус, proposal, declutter (пишется при закрытии окна) |
| `companion.edn` | Опционально: имя **tmux**-сессии companion |

### Команды из UI

| UI | `:op` в `to-agent.edn` |
|----|-------------------------|
| Regen | `:regen` |
| Real diagram | `:context` + `:context real` |
| Proposal | `:context` + `:context proposal`, `:proposal-id`, `:name` |
| Refresh CRAP | `:refresh-crap` + `:target` |
| Refresh Mutation | `:refresh-mutate` + `:target` |
| Refresh All Mutation | `:refresh-mutate-all` + `:target` |
| Omit | `:omit` + `:target` |

Если в `companion.edn` указана tmux-сессия, viewer шлёт в неё wake-сообщение (как Clojure). Без tmux команды **всё равно пишутся** в `to-agent.edn` — агент может читать их вручную.

Companion и скрипт `./uml` автозапускаются **только** из Clojure-репозитория; .NET viewer рассчитан на уже настроенный workflow или ручную работу с почтой.

### Восстановление сессии

```powershell
dotnet run --project src\UmlViewer.App -- `
  --root "D:\path\to\YourSolution" `
  --ir "D:\path\to\glpi-scan.edn" `
  --restart
```

---

## 8. Proposals и сохранение

- Новые / переименованные / удалённые proposals попадают в **`:proposals`** IR-файла.
- Если рядом с IR есть policy (например `glpi-scan.policy.edn` или путь из `:policy` в IR), обновляется и он.

Не коммитьте в git секреты; `.uml-viewer/` обычно локальный runtime-каталог.

---

## 9. Stacked IR

Если в EDN **` :hierarchical false`** и задан **` :diagrams`**, viewer собирает **stacked**-документ (несколько секций на одном полотне), как `compile-document` в Clojure. Declutter и overlay работают на таком IR.

---

## 10. Типичные проблемы

| Симптом | Что проверить |
|---------|----------------|
| «IR file not found» | `--ir` указывает на существующий файл; в IDE — `launchSettings.json` |
| Нет точек C/M | `--root` не тот → нет `.metrics`; или `:ns` в IR не совпадает с `:namespace` в crap.edn |
| `overlay-check` ругается | Перегенерируйте IR или сузьте `:prefix` / `:sources` в policy |
| Regen «not attached» | Нет `companion.edn` / tmux — команда всё равно в `to-agent.edn` |
| Пустая диаграмма | IR без классов в текущем фокусе; drill назад; проверьте `:prefix` |

---

## 11. Отличия от Clojure `uml-viewer`

| Есть в .NET | Только в Clojure |
|-------------|------------------|
| IR EDN/JSON, overlay, proposals, declutter, stacked diagrams | Полный `clj -M:ir` для всех языков |
| Mailbox, regen, context, element ops | Автозапуск companion / Terminal / osascript |
| Detail card, hover рёбер, open source | Пиксельные golden-тесты Quil ↔ canvas |
| Roslyn `scan` для C# | Клавиша **R** reload в Quil (в .NET — авто по mtime) |

---

## 12. Быстрый сценарий (GLPI)

```powershell
# 1. Метрики уже в solution/.metrics (crapper + mutator)

# 2. IR (Clojure)
cd ..\uml-viewer
clj -M:ir examples\glpi.policy.edn target\glpi-scan.edn

# 3. Viewer
cd ..\uml-viewer-dotnet
dotnet run --project src\UmlViewer.App -- `
  --root "D:\path\to\TP.Cmdb.Glpi.Sync.Service" `
  --ir "..\uml-viewer\target\glpi-scan.edn"
```

Дальше: drill по namespace, Class detail по методам с CRAP, Regen при работе с Grok-companion, `--restart` после закрытия окна.

---

[English README](../README.md)
