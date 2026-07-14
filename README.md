# Solaris3Proxy

A lightweight ASP.NET Core (.NET 10) service that **continuously captures the screen**, OCRs a
coordinate and user ID from an on-screen overlay, and serves the latest values over HTTP.

Capture is done at the compositor level via the Wayland screen-share portal — it never hooks into
any application process.

> **Scope:** This is a developer-focused service — a building block that exposes an HTTP API for
> other software to consume. It has **no features for end users or players** and no UI of its own.

> **Platform support:** Linux/Wayland only for now. **Windows support will follow in a later
> update.** (An X11 backend is also planned.) The capture layer is abstracted behind
> `IScreenCapturer`, so additional backends slot in without touching the rest of the app.

---

## How it works

```
xdg-desktop-portal ScreenCast (D-Bus)        ── one-time user consent
    │
    ▼  PipeWire node
GStreamer (pipewiresrc → PNG frames)
    │
    ▼  sampled every 500 ms (configurable)
CoordinateCaptureWorker
    │
    ▼
Skia crop + binarize → Tesseract (single OCR pass, both regions) → regex parse
    │
    ▼
ICoordinateStore (latest only, in-memory) → HTTP endpoints
```

Each cycle crops two relative regions (coordinate — bottom-left; user ID — bottom-right), stacks
them into one image, and runs a **single** Tesseract pass. A frame is only published when **both**
a valid coordinate (`X,Y,Z`) and a user ID (`User ID: <n>`) are read; otherwise it is discarded and
the previous value is kept.

## Requirements

- **.NET 10 SDK**
- A **Linux Wayland** session with a working `xdg-desktop-portal` **ScreenCast** backend
  (e.g. `xdg-desktop-portal-kde` or `-gnome`)
- **PipeWire** and **GStreamer** with the `pipewiresrc` plugin (`gst-plugin-pipewire` /
  `gst-plugins-good`)
- **Tesseract** native libs (`libtesseract`, `libleptonica`) and the English trained data
  (`tesseract-data-eng`, default lookup `/usr/share/tessdata`)

The app auto-creates the versioned native-library symlinks Tesseract's wrapper expects
(`x64/lib…so`) at startup — no manual linking needed.

Quick portal sanity check (should return an object path and **not** crash the portal):

```bash
busctl --user call org.freedesktop.portal.Desktop /org/freedesktop/portal/desktop \
  org.freedesktop.portal.ScreenCast CreateSession 'a{sv}' 2 \
  handle_token s a session_handle_token s b
```

## Run

```bash
dotnet run --project src/Solaris3Proxy
```

The `http` launch profile runs in **Development** on <http://localhost:5000>. On first start,
**approve the screen-share dialog** — a restore token is saved so later starts don't prompt.

- API docs (Scalar, dev only): <http://localhost:5000/scalar/v1>
- OpenAPI document: <http://localhost:5000/openapi/v1.json>

## Endpoints

| Method & path | Description |
|---|---|
| `GET /api/coordinates/latest` | Latest snapshot: `{ capturedAt, extractedAt, success, coordinate: {x,y,z}, userId, confidence, rawText }`. `204` until the first valid read. |
| `GET /api/coordinates/latest/image` | The PNG frame the latest snapshot came from (`image/png`). `204` if none yet. |
| `POST /api/coordinates/extract` | One-off extraction from an uploaded image (multipart form field `image`). Handy for testing the pipeline without live capture. |

```bash
curl http://localhost:5000/api/coordinates/latest
curl -X POST http://localhost:5000/api/coordinates/extract -F image=@screenshot.png
```

## Configuration

Bind via `appsettings.json`, environment variables, or CLI args.

**`ScreenCapture`**

| Key | Default | Description |
|---|---|---|
| `IntervalMilliseconds` | `500` | How often the latest frame is sampled and OCR'd. |
| `FrameRate` | `5` | FPS the capture pipeline produces. |
| `CursorMode` | `1` | Portal cursor mode (1 hidden, 2 embedded, 4 metadata). |
| `FrameDirectory` | temp dir | Where frames are written/read. |
| `ConsentTimeoutSeconds` | `120` | Wait for the consent dialog. |
| `StartMaxAttempts` / `StartRetryDelaySeconds` | `3` / `5` | Retry if the portal is restarting. |

**`CoordinateExtraction`**

| Key | Default | Description |
|---|---|---|
| `RelativeLeft/Top/Right/Bottom` | bottom-left box | Coordinate crop region, as fractions of width/height (resolution-independent). |
| `UserIdRelativeLeft/Top/Right/Bottom` | bottom-right box | User-ID crop region. |
| `LuminanceThreshold` | `62` | Binarization cutoff for the semi-transparent overlay text. |
| `UpscaleFactor` | `4` | Region upscaling before OCR. |
| `TessDataPath` | `/usr/share/tessdata` | Directory containing `eng.traineddata`. |
| `Language` | `eng` | Tesseract language. |

Regions are relative, so the defaults (tuned on a 2560×1440 reference) also work at 1080p, 4K, etc.

## Project layout

```
src/Solaris3Proxy/
├── Program.cs                     # DI wiring, endpoint mapping, OpenAPI/Scalar
├── Endpoints/                     # Minimal-API endpoint groups
├── Services/
│   ├── CoordinateCaptureWorker.cs # BackgroundService: capture → extract → store
│   ├── Impl/CoordinateExtractor.cs# Skia preprocessing + Tesseract + regex
│   └── Screen/                    # IScreenCapturer abstraction + Wayland backend
├── Options/                       # CoordinateExtraction / ScreenCapture options
└── Models/                        # DTOs
```

## Build

```bash
dotnet build --configuration Release
```

## License

Released under the [MIT License](LICENSE). Copyright (c) 2026 Shiron.

## Disclaimer

This project is an independent, unofficial, fan-made developer tool. It is **not affiliated with,
endorsed by, sponsored by, or associated with Kuro Games or Wuthering Waves** in any way.

"Wuthering Waves", "Kuro Games", and all related names, logos, assets, and trademarks are the
property of their respective owners. Their use here is purely nominative — to describe
interoperability — and implies no endorsement or partnership. The software is provided "as is" for
educational and personal use; you are solely responsible for ensuring your use complies with the
terms of service of any third-party software or game you use it with.
