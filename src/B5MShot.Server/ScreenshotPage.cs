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
  <meta name="theme-color" content="#080d18">
  <meta name="description" content="Снимок экрана, созданный в B5MShot">
  <meta property="og:title" content="Снимок B5MShot">
  <meta property="og:description" content="Быстрый и удобный скриншот для Windows">
  <meta property="og:image" content="/raw/__SHOT_ID__">
  <title>Снимок B5MShot</title>
  <style>
    :root{color-scheme:dark;--bg:#070b14;--panel:#101727;--line:#293855;--text:#f7f9ff;--muted:#94a3ba;--blue:#4e8dff;--cyan:#53d7ff;--glow:rgba(78,141,255,.22)}
    *{box-sizing:border-box}
    html,body{height:100%;overflow:hidden}
    html{background:var(--bg)}
    body{margin:0;display:grid;grid-template-rows:64px minmax(0,1fr);color:var(--text);font:15px/1.45 Inter,"Segoe UI",Arial,sans-serif;background:radial-gradient(circle at 50% -18%,rgba(64,116,218,.23),transparent 38%),linear-gradient(180deg,#0b1020 0,#070b14 60%,#080c16 100%)}
    a{color:inherit;text-decoration:none}
    button{font:inherit}
    .header{z-index:10;border-bottom:1px solid rgba(148,163,184,.14);background:rgba(7,11,20,.8);backdrop-filter:blur(18px)}
    .nav{width:min(1280px,calc(100% - 32px));height:64px;margin:auto;display:flex;align-items:center;justify-content:space-between;gap:20px}
    .brand{display:flex;align-items:center;gap:11px;font-size:18px;font-weight:800;letter-spacing:.2px}
    .brand img{width:36px;height:36px;border-radius:10px;box-shadow:0 8px 25px rgba(63,131,255,.24)}
    .nav-actions{display:flex;align-items:center;gap:9px}
    .nav-link{padding:8px 11px;color:#b8c3d7;border-radius:10px;transition:.2s}
    .nav-link:hover{color:#fff;background:rgba(255,255,255,.06)}
    .app-button{padding:9px 14px;border:1px solid #31599d;border-radius:11px;background:rgba(52,103,195,.18);font-weight:700;transition:.2s}
    .app-button:hover{border-color:#4d7bca;background:rgba(52,103,195,.3)}
    .main{width:min(1280px,calc(100% - 32px));height:100%;min-height:0;margin:0 auto;padding:16px 0;display:flex}
    .shot-shell{position:relative;flex:1;min-width:0;min-height:0;padding:1px;border-radius:22px;background:linear-gradient(135deg,rgba(83,215,255,.6),rgba(78,141,255,.18) 42%,rgba(126,87,255,.5));box-shadow:0 26px 70px rgba(0,0,0,.34),0 0 44px var(--glow)}
    .shot-card{height:100%;min-height:0;overflow:hidden;display:grid;grid-template-rows:minmax(0,1fr) auto;border-radius:21px;background:rgba(15,22,37,.97)}
    .image-stage{min-width:0;min-height:0;overflow:hidden;padding:16px;display:flex;align-items:center;justify-content:center;background-color:#0a0f1b;background-image:linear-gradient(45deg,rgba(255,255,255,.025) 25%,transparent 25%),linear-gradient(-45deg,rgba(255,255,255,.025) 25%,transparent 25%),linear-gradient(45deg,transparent 75%,rgba(255,255,255,.025) 75%),linear-gradient(-45deg,transparent 75%,rgba(255,255,255,.025) 75%);background-size:24px 24px;background-position:0 0,0 12px,12px -12px,-12px 0}
    .image-frame{max-width:100%;max-height:100%;min-width:0;min-height:0;padding:6px;display:flex;align-items:center;justify-content:center;border:1px solid rgba(134,164,215,.4);border-radius:14px;background:#111a2a;box-shadow:0 16px 44px rgba(0,0,0,.42)}
    #shot{display:block;max-width:100%;max-height:100%;width:auto;height:auto;object-fit:contain;border-radius:8px;background:#fff}
    .toolbar{position:relative;padding:13px 14px;display:grid;grid-template-columns:minmax(0,1fr) auto;align-items:center;gap:14px;border-top:1px solid rgba(148,163,184,.13);background:linear-gradient(180deg,rgba(19,28,47,.96),rgba(14,21,36,.98))}
    .meta{min-width:0;display:flex;align-items:center;flex-wrap:wrap;gap:7px;color:var(--muted);font-size:12px}
    .pill{padding:5px 8px;border:1px solid rgba(148,163,184,.15);border-radius:8px;background:rgba(255,255,255,.025);white-space:nowrap}
    .actions{display:flex;align-items:center;gap:9px}
    .button{height:44px;padding:0 16px;display:inline-flex;align-items:center;justify-content:center;gap:9px;border:1px solid var(--line);border-radius:12px;color:#eef4ff;background:#182237;cursor:pointer;font-weight:750;white-space:nowrap;transition:transform .16s,border-color .16s,background .16s,box-shadow .16s}
    .button:hover{transform:translateY(-1px);border-color:#4a628d;background:#1d2a43}
    .button:focus-visible,.menu-item:focus-visible{outline:2px solid #77b7ff;outline-offset:2px}
    .button.primary{padding:0 19px;border-color:#75b8ff;background:linear-gradient(135deg,#2872ef 0%,#518cff 58%,#5daeff 100%);box-shadow:0 10px 26px rgba(55,119,239,.3),inset 0 1px rgba(255,255,255,.22)}
    .button.primary:hover{border-color:#a6d4ff;background:linear-gradient(135deg,#347ef7,#6198ff 58%,#69b7ff);box-shadow:0 13px 30px rgba(55,119,239,.4)}
    .button:disabled{opacity:.6;cursor:wait;transform:none}
    .download-icon{width:23px;height:23px;display:inline-grid;place-items:center;border-radius:7px;background:rgba(255,255,255,.17);font-size:17px;line-height:1}
    .save-wrap{position:relative}
    .chevron{font-size:11px;color:#a9bad5;transition:transform .16s}
    .save-button[aria-expanded="true"] .chevron{transform:rotate(180deg)}
    .format-menu{position:absolute;right:0;bottom:calc(100% + 9px);z-index:20;width:190px;padding:6px;border:1px solid #334664;border-radius:13px;background:rgba(17,25,42,.98);box-shadow:0 18px 48px rgba(0,0,0,.5);backdrop-filter:blur(16px)}
    .format-menu[hidden]{display:none}
    .menu-item{width:100%;padding:9px 10px;display:flex;align-items:center;justify-content:space-between;gap:12px;border:0;border-radius:9px;color:#eef4ff;background:transparent;cursor:pointer;text-align:left}
    .menu-item:hover{background:#21304b}
    .menu-format{font-weight:750}
    .menu-note{color:#8292aa;font-size:11px}
    .copy-button{width:44px;padding:0;color:#b9c9df}
    .copy-icon{font-size:19px;line-height:1}
    .notice{position:fixed;right:20px;bottom:20px;z-index:30;max-width:min(380px,calc(100% - 40px));padding:11px 14px;border:1px solid rgba(94,221,179,.28);border-radius:11px;color:#aef3d9;background:rgba(15,33,35,.94);box-shadow:0 14px 36px rgba(0,0,0,.38);font-size:13px;animation:notice-in .18s ease-out}
    .notice:empty{display:none}
    @keyframes notice-in{from{opacity:0;transform:translateY(7px)}to{opacity:1;transform:none}}
    @media (max-width:760px){body{grid-template-rows:58px minmax(0,1fr)}.nav{width:calc(100% - 20px);height:58px}.nav-link{display:none}.main{width:calc(100% - 20px);padding:10px 0}.shot-shell{border-radius:17px}.shot-card{border-radius:16px}.image-stage{padding:9px}.toolbar{grid-template-columns:1fr;padding:9px;gap:8px}.meta{justify-content:center}.actions{display:grid;grid-template-columns:1fr 1fr 44px}.button{height:42px;padding:0 11px}.button.primary{padding:0 13px}.save-wrap,.save-button{width:100%}.format-menu{right:0}.notice{right:10px;bottom:10px}}
    @media (max-width:470px){.brand{gap:8px}.brand img{width:33px;height:33px}.brand span{font-size:16px}.app-button{padding:8px 10px;font-size:12px}.app-label-long{display:none}.main{width:calc(100% - 12px);padding:6px 0}.image-stage{padding:6px}.pill:first-child{display:none}.actions{gap:6px}.button{font-size:13px}.download-icon{width:20px;height:20px}.format-menu{width:176px}}
    @media (max-height:520px){body{grid-template-rows:52px minmax(0,1fr)}.nav{height:52px}.brand img{width:32px;height:32px}.main{padding:6px 0}.toolbar{padding:7px 9px}.button{height:38px}.meta{display:none}.image-stage{padding:6px}}
  </style>
</head>
<body>
  <header class="header">
    <div class="nav">
      <a class="brand" href="/" aria-label="B5MShot — главная"><img src="/assets/logo.png" alt=""><span>B5MShot</span></a>
      <nav class="nav-actions" aria-label="Навигация"><a class="nav-link" href="/">О программе</a><a class="app-button" href="/download/B5MShot-Setup.exe">Скачать <span class="app-label-long">приложение</span></a></nav>
    </div>
  </header>
  <main class="main">
    <section class="shot-shell" aria-label="Снимок B5MShot">
      <div class="shot-card">
        <div class="image-stage"><div class="image-frame"><img id="shot" src="/raw/__SHOT_ID__" alt="Снимок B5MShot" decoding="async"></div></div>
        <div class="toolbar">
          <div class="meta"><span class="pill">Оригинал: __FORMAT_LABEL__</span><span class="pill">__SIZE_LABEL__</span><span class="pill" id="dimensions">Определяем размер…</span></div>
          <div class="actions">
            <button class="button primary" id="quickPng" type="button"><span class="download-icon" aria-hidden="true">↓</span><span>Скачать PNG</span></button>
            <div class="save-wrap">
              <button class="button save-button" id="saveAs" type="button" aria-haspopup="menu" aria-expanded="false"><span>Сохранить как</span><span class="chevron" aria-hidden="true">▾</span></button>
              <div class="format-menu" id="formatMenu" role="menu" hidden>
                <button class="menu-item" type="button" role="menuitem" data-format="png"><span class="menu-format">PNG</span><span class="menu-note">без потерь</span></button>
                <button class="menu-item" type="button" role="menuitem" data-format="jpeg"><span class="menu-format">JPG</span><span class="menu-note">меньше размер</span></button>
                <button class="menu-item" type="button" role="menuitem" data-format="webp"><span class="menu-format">WebP</span><span class="menu-note">современный</span></button>
              </div>
            </div>
            <button class="button copy-button" id="copyLink" type="button" aria-label="Копировать ссылку" title="Копировать ссылку"><span class="copy-icon" aria-hidden="true">⧉</span></button>
          </div>
        </div>
      </div>
    </section>
    <div class="notice" id="notice" role="status" aria-live="polite"></div>
  </main>
  <script>
    (() => {
      const shotId = "__SHOT_ID__";
      const originalExtension = "__ORIGINAL_EXTENSION__";
      const image = document.getElementById("shot");
      const quickButton = document.getElementById("quickPng");
      const saveAsButton = document.getElementById("saveAs");
      const formatMenu = document.getElementById("formatMenu");
      const notice = document.getElementById("notice");
      let noticeTimer;

      const showNotice = message => {
        clearTimeout(noticeTimer);
        notice.textContent = message;
        noticeTimer = setTimeout(() => { notice.textContent = ""; }, 2800);
      };

      const closeFormatMenu = () => {
        formatMenu.hidden = true;
        saveAsButton.setAttribute("aria-expanded", "false");
      };

      const toggleFormatMenu = () => {
        const willOpen = formatMenu.hidden;
        formatMenu.hidden = !willOpen;
        saveAsButton.setAttribute("aria-expanded", String(willOpen));
        if (willOpen) formatMenu.querySelector(".menu-item")?.focus();
      };

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
        saveAsButton.disabled = true;
        showNotice("Подготавливаем файл…");
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
          showNotice("Файл готов к сохранению");
        } catch {
          showNotice("Не удалось подготовить этот формат. Попробуйте PNG.");
        } finally {
          quickButton.disabled = false;
          saveAsButton.disabled = false;
        }
      };

      quickButton.addEventListener("click", () => downloadAs("png"));
      saveAsButton.addEventListener("click", toggleFormatMenu);
      formatMenu.addEventListener("click", event => {
        const option = event.target.closest("[data-format]");
        if (!option) return;
        closeFormatMenu();
        downloadAs(option.dataset.format);
      });
      document.addEventListener("click", event => {
        if (!event.target.closest(".save-wrap")) closeFormatMenu();
      });
      document.addEventListener("keydown", event => {
        if (event.key === "Escape") {
          closeFormatMenu();
          saveAsButton.focus();
        }
      });
      document.getElementById("copyLink").addEventListener("click", async () => {
        try {
          await navigator.clipboard.writeText(location.href);
          showNotice("Ссылка скопирована");
        } catch {
          showNotice("Не удалось скопировать ссылку");
        }
      });
    })();
  </script>
</body>
</html>
""";
}
