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
  <meta name="theme-color" content="#edf3fa">
  <meta name="description" content="Снимок экрана, созданный в B5MShot">
  <meta property="og:title" content="Снимок B5MShot">
  <meta property="og:description" content="Быстрый и удобный скриншот для Windows">
  <meta property="og:image" content="/raw/__SHOT_ID__">
  <title>Снимок B5MShot</title>
  <link rel="stylesheet" href="/assets/viewer.css?v=3"><link rel="icon" href="/assets/logo.svg?v=3" type="image/svg+xml">
</head>
<body>
  <header class="header">
    <div class="nav">
      <a class="brand" href="/" aria-label="B5MShot — главная"><img src="/assets/logo.svg?v=3" alt=""><span>B5MShot</span></a>
      <nav class="nav-actions" aria-label="Навигация"><a class="nav-link" href="/">О программе</a><a class="app-button" href="/#download">Скачать <span class="app-label-long">приложение</span></a></nav>
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
