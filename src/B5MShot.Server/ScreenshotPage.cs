using System.Globalization;

public static class ScreenshotPage
{
    public static string Render(string id, ImageType imageType, long fileSize)
    {
        var sizeLabel = FormatFileSize(fileSize);
        var formatLabel = imageType == ImageTypes.Png ? "PNG" : "JPG";

        return Html
            .Replace("__SHOT_ID__", id, StringComparison.Ordinal)
            .Replace("__ORIGINAL_EXTENSION__", imageType.Extension, StringComparison.Ordinal)
            .Replace("__FORMAT_LABEL__", formatLabel, StringComparison.Ordinal)
            .Replace("__SIZE_LABEL__", sizeLabel, StringComparison.Ordinal);
    }

    private static string FormatFileSize(long bytes)
    {
        if (bytes >= 1024 * 1024)
        {
            return $"{bytes / 1024d / 1024d:0.##} МБ".Replace('.', ',');
        }

        if (bytes >= 1024)
        {
            return $"{bytes / 1024d:0.#} КБ".Replace('.', ',');
        }

        return string.Create(CultureInfo.InvariantCulture, $"{bytes} Б");
    }

    private const string Html = """
<!doctype html>
<html lang="ru">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width,initial-scale=1">
  <meta name="theme-color" content="#0b1020">
  <meta name="description" content="Снимок экрана, созданный в B5MShot">
  <meta property="og:title" content="Снимок B5MShot">
  <meta property="og:description" content="Быстрый и удобный скриншот для Windows">
  <meta property="og:image" content="/raw/__SHOT_ID__">
  <title>Снимок B5MShot</title>
  <style>
    :root{color-scheme:dark;--bg:#070b14;--panel:#101727;--panel2:#151e31;--line:#273450;--text:#f7f9ff;--muted:#94a3ba;--blue:#4e8dff;--cyan:#53d7ff;--glow:rgba(78,141,255,.22)}
    *{box-sizing:border-box}
    html{min-height:100%;background:var(--bg)}
    body{min-height:100vh;margin:0;color:var(--text);font:15px/1.5 Inter,"Segoe UI",Arial,sans-serif;background:radial-gradient(circle at 50% -10%,rgba(64,116,218,.2),transparent 35%),linear-gradient(180deg,#0b1020 0,#070b14 48%,#080c16 100%)}
    a{color:inherit;text-decoration:none}
    button,select{font:inherit}
    .header{position:sticky;top:0;z-index:10;border-bottom:1px solid rgba(148,163,184,.14);background:rgba(7,11,20,.78);backdrop-filter:blur(18px)}
    .nav{width:min(1180px,calc(100% - 32px));height:72px;margin:auto;display:flex;align-items:center;justify-content:space-between;gap:24px}
    .brand{display:flex;align-items:center;gap:12px;font-size:19px;font-weight:800;letter-spacing:.2px}
    .brand img{width:38px;height:38px;border-radius:11px;box-shadow:0 8px 25px rgba(63,131,255,.24)}
    .nav-actions{display:flex;align-items:center;gap:10px}
    .nav-link{padding:9px 12px;color:#b8c3d7;border-radius:10px;transition:.2s}
    .nav-link:hover{color:#fff;background:rgba(255,255,255,.06)}
    .app-button{padding:10px 15px;border:1px solid #31599d;border-radius:11px;background:rgba(52,103,195,.18);font-weight:700}
    .main{width:min(1180px,calc(100% - 32px));margin:0 auto;padding:52px 0 44px}
    .intro{text-align:center;margin-bottom:25px}
    .eyebrow{display:inline-flex;align-items:center;gap:8px;padding:6px 10px;border:1px solid rgba(83,215,255,.2);border-radius:99px;background:rgba(83,215,255,.07);color:#a8eaff;font-size:12px;font-weight:700;letter-spacing:.6px;text-transform:uppercase}
    .eyebrow:before{content:"";width:7px;height:7px;border-radius:50%;background:#55e2b2;box-shadow:0 0 12px #55e2b2}
    h1{margin:15px 0 7px;font-size:clamp(29px,4vw,44px);line-height:1.12;letter-spacing:-1.3px}
    .subtitle{margin:0;color:var(--muted)}
    .shot-shell{position:relative;padding:1px;border-radius:22px;background:linear-gradient(135deg,rgba(83,215,255,.58),rgba(78,141,255,.18) 42%,rgba(126,87,255,.48));box-shadow:0 30px 80px rgba(0,0,0,.35),0 0 48px var(--glow)}
    .shot-card{overflow:hidden;border-radius:21px;background:rgba(15,22,37,.97)}
    .image-stage{min-height:240px;padding:24px;display:grid;place-items:center;background-color:#0a0f1b;background-image:linear-gradient(45deg,rgba(255,255,255,.025) 25%,transparent 25%),linear-gradient(-45deg,rgba(255,255,255,.025) 25%,transparent 25%),linear-gradient(45deg,transparent 75%,rgba(255,255,255,.025) 75%),linear-gradient(-45deg,transparent 75%,rgba(255,255,255,.025) 75%);background-size:24px 24px;background-position:0 0,0 12px,12px -12px,-12px 0}
    .image-frame{max-width:100%;padding:7px;border:1px solid rgba(134,164,215,.35);border-radius:13px;background:#111a2a;box-shadow:0 18px 48px rgba(0,0,0,.42)}
    #shot{display:block;max-width:100%;max-height:68vh;border-radius:7px;background:#fff}
    .toolbar{padding:20px 22px;display:flex;align-items:center;justify-content:space-between;gap:18px;border-top:1px solid rgba(148,163,184,.13)}
    .meta{display:flex;flex-wrap:wrap;gap:8px;color:var(--muted);font-size:13px}
    .pill{padding:6px 9px;border:1px solid rgba(148,163,184,.15);border-radius:8px;background:rgba(255,255,255,.025)}
    .actions{display:flex;align-items:center;gap:10px;flex-wrap:wrap;justify-content:flex-end}
    .button{min-height:42px;padding:0 16px;display:inline-flex;align-items:center;justify-content:center;gap:8px;border:1px solid var(--line);border-radius:11px;color:#eef4ff;background:#182237;cursor:pointer;font-weight:700;transition:transform .16s,border-color .16s,background .16s}
    .button:hover{transform:translateY(-1px);border-color:#4a628d;background:#1c2941}
    .button.primary{border-color:#5b98ff;background:linear-gradient(135deg,#3979ed,#5a8dff);box-shadow:0 10px 26px rgba(55,119,239,.25)}
    .button:disabled{opacity:.6;cursor:wait;transform:none}
    .format-box{height:42px;padding:0 34px 0 12px;border:1px solid var(--line);border-radius:11px;color:#eef4ff;background:#101a2c;cursor:pointer}
    .notice{min-height:24px;margin:13px 2px 0;color:#82e5c3;text-align:right;font-size:13px}
    .footer{padding:25px 0 0;text-align:center;color:#66758f;font-size:12px}
    @media (max-width:760px){.nav{height:64px}.nav-link{display:none}.app-button{padding:9px 11px}.main{padding-top:31px}.image-stage{padding:12px}.toolbar{align-items:stretch;flex-direction:column}.actions{justify-content:stretch}.actions>*{flex:1}.button{white-space:nowrap}.notice{text-align:center}}
    @media (max-width:470px){.brand span{font-size:17px}.app-button{font-size:12px}.main{width:min(100% - 20px,1180px)}.actions{display:grid;grid-template-columns:1fr 1fr}.actions .primary{grid-column:1/-1}.image-stage{min-height:170px}}
  </style>
</head>
<body>
  <header class="header">
    <div class="nav">
      <a class="brand" href="/" aria-label="B5MShot — главная"><img src="/assets/logo.png" alt=""><span>B5MShot</span></a>
      <nav class="nav-actions" aria-label="Навигация"><a class="nav-link" href="/">О программе</a><a class="app-button" href="/download/B5MShot.exe">Скачать приложение</a></nav>
    </div>
  </header>
  <main class="main">
    <section class="intro"><span class="eyebrow">Снимок доступен</span><h1>Ваш снимок готов</h1><p class="subtitle">Откройте в полном размере или скачайте в нужном формате</p></section>
    <section class="shot-shell">
      <div class="shot-card">
        <div class="image-stage"><div class="image-frame"><img id="shot" src="/raw/__SHOT_ID__" alt="Снимок B5MShot" decoding="async"></div></div>
        <div class="toolbar">
          <div class="meta"><span class="pill">Оригинал: __FORMAT_LABEL__</span><span class="pill">__SIZE_LABEL__</span><span class="pill" id="dimensions">Загрузка размера…</span></div>
          <div class="actions">
            <button class="button primary" id="quickPng" type="button">↓ Скачать PNG</button>
            <select class="format-box" id="format" aria-label="Формат файла"><option value="jpeg">JPG</option><option value="webp">WebP</option><option value="png">PNG</option></select>
            <button class="button" id="downloadSelected" type="button">Скачать</button>
            <button class="button" id="copyLink" type="button">Копировать ссылку</button>
          </div>
        </div>
      </div>
    </section>
    <div class="notice" id="notice" role="status" aria-live="polite"></div>
    <footer class="footer">Создано с помощью B5MShot · быстрые скриншоты для Windows</footer>
  </main>
  <script>
    (() => {
      const shotId = "__SHOT_ID__";
      const originalExtension = "__ORIGINAL_EXTENSION__";
      const image = document.getElementById("shot");
      const quickButton = document.getElementById("quickPng");
      const selectedButton = document.getElementById("downloadSelected");
      const notice = document.getElementById("notice");
      const format = document.getElementById("format");

      image.addEventListener("load", () => {
        document.getElementById("dimensions").textContent = `${image.naturalWidth} × ${image.naturalHeight} px`;
      }, { once: true });

      const saveBlob = (blob, extension) => {
        const link = document.createElement("a");
        const url = URL.createObjectURL(blob);
        link.href = url;
        link.download = `B5MShot-${shotId}.${extension}`;
        document.body.appendChild(link);
        link.click();
        link.remove();
        setTimeout(() => URL.revokeObjectURL(url), 1000);
      };

      const downloadAs = async type => {
        quickButton.disabled = true;
        selectedButton.disabled = true;
        notice.textContent = "Подготавливаем файл…";
        try {
          if (type === "png" && originalExtension === "png") {
            const link = document.createElement("a");
            link.href = `/raw/${shotId}?download=true`;
            link.download = `B5MShot-${shotId}.png`;
            document.body.appendChild(link);
            link.click();
            link.remove();
          } else {
            await image.decode();
            const canvas = document.createElement("canvas");
            canvas.width = image.naturalWidth;
            canvas.height = image.naturalHeight;
            const context = canvas.getContext("2d");
            if (!context) throw new Error("Canvas is unavailable");
            if (type === "jpeg") {
              context.fillStyle = "#ffffff";
              context.fillRect(0, 0, canvas.width, canvas.height);
            }
            context.drawImage(image, 0, 0);
            const mime = type === "jpeg" ? "image/jpeg" : `image/${type}`;
            const blob = await new Promise(resolve => canvas.toBlob(resolve, mime, .92));
            if (!blob) throw new Error("Format is unavailable");
            saveBlob(blob, type === "jpeg" ? "jpg" : type);
          }
          notice.textContent = "Файл готов к сохранению";
        } catch {
          notice.textContent = "Не удалось подготовить этот формат. Попробуйте PNG.";
        } finally {
          quickButton.disabled = false;
          selectedButton.disabled = false;
        }
      };

      quickButton.addEventListener("click", () => downloadAs("png"));
      selectedButton.addEventListener("click", () => downloadAs(format.value));
      document.getElementById("copyLink").addEventListener("click", async () => {
        try {
          await navigator.clipboard.writeText(location.href);
          notice.textContent = "Ссылка скопирована";
        } catch {
          notice.textContent = "Не удалось скопировать ссылку";
        }
      });
    })();
  </script>
</body>
</html>
""";
}
