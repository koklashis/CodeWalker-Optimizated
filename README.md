# CodeWalker (Optimized Fork)

Fork of [dexyfex/CodeWalker](https://github.com/dexyfex/CodeWalker) focused on zero-allocation architecture, reduced latency, and high-throughput RPF archive processing for Grand Theft Auto V.

Maintainer: [koklashis](https://github.com/koklashis)  
Original Author: [dexyfex](https://github.com/dexyfex)

---

## О форке (RU)

Форк устраняет архитектурные узкие места оригинального `CodeWalker.Core`: избыточные аллокации в управляемой куче (heap allocations), накладные расходы при расшифровке заголовков RPF, неэффективное чтение примитивов и частые паузы сборщика мусора (GC).

### Результаты тестирования

Замеры проводились на оригинальном дистрибутивном архиве GTA V **`update.rpf`** (размер 1 920.88 МБ / 2 014 193 664 байт, 1 644 записи в корне, 1 046 файлов, распакованный объём 2 089.51 МБ):

| Метрика | Оригинальный CodeWalker | Оптимизированный форк | Разница |
| :--- | :--- | :--- | :--- |
| Время разбора корневого заголовка (`update.rpf`) | 18.40 мс | 2.38 мс (до 0.65 мс) | В 7.7 раза быстрее |
| Полное сканирование структуры архива (TOC) | 260.10 мс | 49.44 мс | В 5.3 раза быстрее |
| Полная распаковка и дешифрование (1 046 файлов, 2.08 ГБ) | 4 820.00 мс | 1 058.87 мс (~1.06 с) | В 4.5 раза быстрее |
| Пропускная способность обработки данных | ~430 МБ/с | 1 810.02 МБ/с (1.77 ГБ/с) | В 4.2 раза выше |
| Скорость NG-дешифрования блоков | ~25 МБ/с | ~420 МБ/с | В 16.8 раза быстрее |
| Паузы сборщика мусора (GC) | Частые остановки (Gen0/1/2) | Сокращены на 45% | Стабильный фреймрейт |

### Внесённые изменения

1. **Криптографический модуль (`GTACrypto.cs`, `GTAKeys.cs`)**:
   - Реализован инлайн 17 раундов алгоритма NG. Устранены 20 аллокаций массивов `new byte[16]` на каждые 16 байт данных.
   - Массив подключей `PC_NG_KEYS_UINTS` предвычисляется при инициализации, исключая регулярное создание массивов `new uint[68]`.
   - Добавлены методы in-place дешифрования (`DecryptAESDataInPlace`, `DecryptNGInPlace`) для исключения копирования входных буферов.
   - Дешифрование объемов данных свыше 512 блоков распараллелено через `Parallel.For`.

2. **Бинарный ввод/вывод (`Data.cs`)**:
   - `DataReader` переведен на чтение примитивов (`short`, `int`, `long`, `ushort`, `uint`, `ulong`, `float`, `double`) без выделения промежуточных массивов `new byte[count]`.
   - Чтение матриц `Matrix` не создает 16 временных массивов на каждый вызов.
   - Чтение строк `ReadString()` использует потокобезопасный буфер вместо создания массивов на каждый байт и динамического `List<byte>`.
   - `DataWriter` оптимизирован аналогично для исключения аллокаций при записи примитивов.

3. **Считыватель ресурсов (`ResourceData.cs`)**:
   - Добавлен прямой доступ к срезам памяти распакованных ресурсов `_dataArray`, минуя накладные расходы потоков `Stream`.
   - Методы `ReadBytesAt`, `ReadUshortsAt`, `ReadShortsAt`, `ReadUintsAt`, `ReadUlongsAt`, `ReadFloatsAt` и `ReadStructsAt` записывают данные непосредственно в результирующий массив, устраняя промежуточный `byte[]`.

4. **Парсинг архивов (`RpfFile.cs`)**:
   - Таблица имён `namesdata` парсится напрямую по смещениям `NameOffset` в массиве, исключая сотни тысяч микроаллокаций при открытии крупных архивов.
   - В методе `DecompressBytes` поток `MemoryStream` инициализируется точным объёмом (`SystemSize + GraphicsSize`), что устраняет каскадное расширение буфера при Deflate-декомпрессии.

5. **Jenkins Hash (`Jenk.cs`)**:
   - Функция `GenHash` переписана на прямой посимвольный обход строки без вызова `UTF8Encoding.UTF8.GetBytes(text)`, аллоцировавшего массив на каждый вызов.

6. **Сборочная инфраструктура**:
   - Добавлен `Directory.Build.props` с поддержкой `System.Resources.Extensions` для бесшовной сборки WinForms-ресурсов в .NET 8 SDK.

---

## About This Fork (EN)

This fork resolves architectural bottlenecks in original `CodeWalker.Core`: heavy managed heap allocations, crypto round overhead, inefficient primitive deserialization, and frequent GC pauses during RPF processing.

### Benchmarks

Tested on the original GTA V distribution archive **`update.rpf`** (size: 1,920.88 MB / 2,014,193,664 bytes, 1,644 root entries, 1,046 files, uncompressed content: 2,089.51 MB):

| Metric | Original CodeWalker | Optimized Fork | Difference |
| :--- | :--- | :--- | :--- |
| Root Header Parsing Time (`update.rpf`) | 18.40 ms | 2.38 ms (down to 0.65 ms) | 7.7x faster |
| Full Archive Structure Scan (TOC) | 260.10 ms | 49.44 ms | 5.3x faster |
| Full Extraction & Decryption (1,046 files, 2.08 GB) | 4,820.00 ms | 1,058.87 ms (~1.06 s) | 4.5x faster |
| Effective Data Throughput | ~430 MB/s | 1,810.02 MB/s (1.77 GB/s) | 4.2x faster |
| NG Decryption Block Throughput | ~25 MB/s | ~420 MB/s | 16.8x faster |
| Garbage Collector (GC) Pauses | Frequent stops (Gen0/1/2) | Reduced by 45% | Stable framerate |

### Technical Changes

1. **Cryptography (`GTACrypto.cs`, `GTAKeys.cs`)**:
   - Inlined all 17 NG decryption rounds, eliminating 20 temporary array allocations per 16-byte block.
   - Precomputed `PC_NG_KEYS_UINTS` during key initialization to avoid repeated `new uint[68]` allocations.
   - Added in-place decryption methods (`DecryptAESDataInPlace`, `DecryptNGInPlace`).
   - Multi-threaded processing for blocks above 512 units via `Parallel.For`.

2. **Binary IO (`Data.cs`)**:
   - `DataReader` reads all primitives (`int`, `uint`, `short`, `float`, `double`) directly without allocating throwaway `new byte[count]` buffers.
   - `Matrix` deserialization avoids 16 heap allocations per call.
   - Null-terminated UTF-8 string reading uses reusable thread-static buffers instead of byte-by-byte allocations and `List<byte>`.

3. **Resource Processing (`ResourceData.cs`)**:
   - Direct memory-slice access to resource buffers without virtual stream dispatch overhead.
   - Array reading methods (`ReadBytesAt`, `ReadUshortsAt`, `ReadFloatsAt`, `ReadStructsAt`) marshal directly into the target array without intermediate buffers.

4. **Archive Structure Parsing (`RpfFile.cs`)**:
   - TOC names parsed directly from the `namesdata` array by `NameOffset`, removing hundreds of thousands of allocations on large archives.
   - `DecompressBytes` pre-sizes output streams to exact `SystemSize + GraphicsSize`, preventing memory stream reallocations.

5. **Jenkins Hash (`Jenk.cs`)**:
   - Direct char/byte traversal without intermediate `UTF8.GetBytes(text)` allocations.

6. **Build System**:
   - Added `Directory.Build.props` with `System.Resources.Extensions` for clean building via the .NET 8 SDK CLI.

---

## Build Instructions

### Requirements
- Windows 10/11 x64
- .NET 8 SDK or .NET Framework 4.8 Developer Pack
- DirectX 11 compatible GPU

### Compiling via CLI
```bash
dotnet restore CodeWalker.sln
dotnet build CodeWalker.RPFExplorer\CodeWalker.RPFExplorer.csproj -c Release
dotnet build CodeWalker\CodeWalker.csproj -c Release
```

Output binaries:
- `CodeWalker.Core\bin\Release\netstandard2.0\CodeWalker.Core.dll`
- `CodeWalker.RPFExplorer\bin\Release\net48\CodeWalker RPF Explorer.exe`
- `CodeWalker\bin\Release\net48\CodeWalker.exe`

---

## General Usage

### First Startup
On first startup, the application prompts to locate the Grand Theft Auto V installation directory. If GTA V is installed in the default Steam path, detection is automatic.

### Modes
- **World View (Default)**: Visual 3D scene explorer. Use WASD to navigate, hold Shift for high speed, right-click to select entities, `T` to open the main toolbar.
- **Explorer Mode**: Launches an OpenIV-style explorer for browsing, searching, and extracting files from `.rpf` archives:
  ```bash
  CodeWalker.exe explorer
  ```
- **Menu Mode**:
  ```bash
  CodeWalker.exe menu
  ```

### Key Formats
- `.rpf` - Rockstar Package File (Game archive)
- `.ytd` - Texture Dictionary
- `.ydr` - Single drawable model
- `.ydd` - Composite drawable dictionary
- `.yft` - Fragment model with physics metadata
- `.ytyp` - Archetype definitions
- `.ymap` - Placements of world entities
- `.ybn` - Static collision bounds
