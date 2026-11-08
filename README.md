# Задание 6. WebAR-сцена в Unity (De-Panther unity-webxr-export)

Unity **6000.6.6f1**, URP 17.6, WebXR Export **0.25.0** + WebXR Interactions **0.25.0** (OpenUPM).

## Что сделано

| Требование | Где / как |
|---|---|
| Установлен unity-webxr-export | `Packages/manifest.json`: scoped registry OpenUPM, `com.de-panther.webxr`, `com.de-panther.webxr-interactions` |
| Подключён корректно | XR Plug-in Management → WebGL → loader **WebXR Export** (`Assets/XR/…`), `WebXRSettings` (AR: reference space `local`, optional `hit-test`), URP + Linear (требования пакета для Unity 6) |
| Шаблон WebXR | `Assets/WebGLTemplates/WebXRFullView2020` (скопирован из пакета, аналог *Window → WebXR → Copy WebGLTemplates*) — кнопки «AR»/«VR» на странице |
| Камера WebXR + WebXRController | В сцене экземпляр префаба **WebXRCameraSet** (Main Camera + `WebXRCameraSettings` + TrackedPoseDriver, `handL`/`handR` с `WebXRController`) |
| Объект в центре экрана (viewer-space) | Prefab `Assets/Prefabs/ARCube.prefab` (куб 15 см) + `Assets/Scripts/ViewerSpacePlacer.cs`: при старте и при каждом `WebXRManager.OnXRChange` ставит куб на 0.6 м по лучу взгляда камеры; куб остаётся в пространстве WebXRManager (опция `followViewer` — режим HUD) |
| WebGL-настройки | `Assets/Editor/WebARBuilder.cs → SetupPlayerSettings`: Compression = Gzip, **Decompression Fallback = off**, **Multithreading (threadsSupport) = off**, WebGL 2 only, Linear |
| HTTPS-сервер | `server/serve.js` (Node) + сертификат mkcert; отдаёт `.gz` с `Content-Encoding: gzip` (нужно, т.к. fallback выключен) |

Сцена: `Assets/Scenes/WebARScene.unity`. Пост-эффекты, тени на дальних дистанциях, HDR и MSAA выключены для упрощения.

## Найденные проблемы совместимости (и решения)

1. **`ReferenceError: Browser is not defined`** при старте на Unity 6.6 с релизом WebXR Export 0.25.0 (OpenUPM).
   В Unity 6.6 новый Emscripten, где `Browser.mainLoop` вынесен в объект `MainLoop`. Исправлено в master-ветке
   De-Panther (коммит `1f7a06c`), но не в релизе. Решение: пакет `com.de-panther.webxr` встроен в проект
   (`Packages/com.de-panther.webxr`, копия master `1f7a06c`); `webxr-interactions` — из OpenUPM 0.25.0.
2. **Длинные пути Windows (MAX_PATH 260)**: если проект лежит глубоко, Unity не может распаковать пакеты в
   `Library/PackageCache`. Решение — держать проект в коротком пути или смонтировать его как диск:
   `subst W: <путь к проекту>` и открывать `W:\`.
3. **Брандмауэр Windows** может блокировать порт 8443 для телефона — разрешите входящие подключения для Node.js.

## Как пересобрать

В редакторе: меню **WebAR Demo → 1. Setup Project → 2. Create Scene → 3. Build WebGL**.

Из командной строки:

```bash
"C:\Program Files\Unity\Hub\Editor\6000.6.6f1\Editor\Unity.exe" -batchmode -nographics -projectPath . -buildTarget WebGL -executeMethod WebARDemo.Editor.WebARBuilder.SetupAndBuild -webarCompression Gzip -logFile build.log
```

Сборка → `Builds/WebGL_Gzip`, отчёт о размерах → `Builds/size_Gzip.txt`.

## Запуск на HTTPS

```bash
node server/serve.js Builds/WebGL_Gzip 8443
```

* ПК: `https://localhost:8443/`
* Телефон (та же Wi-Fi сеть): `https://<IP ПК>:8443/` (адрес печатается при запуске).
  Сертификат выпущен mkcert для `localhost`, `127.0.0.1` и LAN-IP. Чтобы телефон ему доверял, установите
  корневой сертификат `rootCA.pem` из `mkcert -CAROOT` (Android: *Настройки → Безопасность → Шифрование → Установить сертификат CA*;
  iOS не поддерживает WebXR AR в Safari). Либо в Chrome нажмите «Дополнительно → Перейти на сайт».
  Альтернатива без сертификатов: USB-отладка + `chrome://inspect` → *Port forwarding* 8443 → `localhost:8443`
  (localhost — secure context).
* Если не открывается с телефона — разрешите входящие на порт 8443 в брандмауэре Windows.

## Тестирование (чек-лист)

1. **Десктопный браузер (Chrome/Edge)**: сцена грузится, куб виден в центре экрана и вращается; кнопка AR неактивна (нет WebXR AR), VR — только при подключённой гарнитуре/эмуляторе.
   Для проверки AR на ПК можно поставить расширение *Immersive Web Emulator* (Meta) — оно эмулирует сессию `immersive-ar`.
2. **Android + Chrome (ARCore / Google Play Services for AR)**: открыть страницу → «AR» → разрешить камеру → куб появляется в 0.6 м перед телефоном, в центре экрана, и остаётся на месте в мире при перемещении телефона.
3. Логи — `chrome://inspect` → консоль: `[ViewerSpacePlacer] XR state: AR …`, `placed at …`.

## Совместимость

| Платформа | AR (`immersive-ar`) | VR | Обычный 3D-режим |
|---|---|---|---|
| Android Chrome 81+ с ARCore | ✅ | — | ✅ |
| Android Samsung Internet | ✅ (новые версии) | — | ✅ |
| Android Firefox | ❌ (нет WebXR) | ❌ | ✅ |
| iOS Safari / Chrome (WebKit) | ❌ (WebXR AR не поддерживается) | ❌ | ✅ |
| Apple Vision Pro Safari | ❌ AR / ✅ VR | ✅ | ✅ |
| Meta Quest Browser | ✅ (passthrough) | ✅ | ✅ |
| Desktop Chrome/Edge | ❌ (эмулятор — ✅) | ✅ с гарнитурой | ✅ |

Требования: HTTPS (secure context), WebGL 2, отключённый multithreading (SharedArrayBuffer потребовал бы COOP/COEP-заголовков).

## Размер сборки

Unity 6000.6.6f1, URP, Managed Stripping = Low, Strip Engine Code = on, без fallback и многопоточности
(подробно — `Builds/size_*.txt`):

| Сжатие | .wasm | .data | framework.js | Всего |
|---|---|---|---|---|
| Disabled | 37,7 МБ | 12,4 МБ | 417 КБ | **50,6 МБ** |
| Gzip (основная) | 10,2 МБ | 5,0 МБ | 90 КБ | **15,3 МБ** |
| Brotli | 7,5 МБ | 4,1 МБ | 80 КБ | **11,7 МБ** |

Выводы:
* Brotli на ~24 % меньше Gzip, но работает только по HTTPS и требует `Content-Encoding: br` от сервера;
  Gzip поддерживается любым сервером — выбран как основной. Без fallback сервер **обязан** отдавать `Content-Encoding`,
  иначе загрузка падает (наш `serve.js` это делает).
* Основной вес — код движка + URP (wasm) и шейдеры/ресурсы URP (data); сам куб и скрипты весят килобайты.
* Как уменьшить: Managed Stripping = High, отключить неиспользуемые модули (Physics, Audio, UI, Video),
  убрать webxr-interactions (руки/модели контроллеров), вариант Shader Stripping в URP. Для мобильной сети 12–15 МБ —
  загрузка 5–15 с на 4G; после первой загрузки работает кеш (Data Caching = on).

## Результаты тестирования

| Где | Результат |
|---|---|
| Desktop Chromium (встроенный браузер, HTTPS localhost) | ✅ сцена загружается, WebXR-подсистема стартует, лог `[ViewerSpacePlacer] placed at (0.00, 0.00, 0.60)`, куб в центре экрана; кнопки AR/VR неактивны (нет WebXR-устройства) — ожидаемо |
| Android + Chrome (AR) | заполнить после проверки на телефоне: устройство, версия Chrome, появился ли куб в центре, FPS |
