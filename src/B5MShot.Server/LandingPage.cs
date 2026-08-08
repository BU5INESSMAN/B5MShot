public static class LandingPage
{
    public const string Html = """
<!doctype html>
<html lang="ru">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width,initial-scale=1">
  <meta name="theme-color" content="#171b26">
  <meta name="description" content="B5MShot — быстрые скриншоты Windows с редактором, горячими клавишами и загрузкой в облако.">
  <meta property="og:type" content="website">
  <meta property="og:url" content="https://s.bu5inessman.ru/">
  <meta property="og:title" content="B5MShot — скриншоты без лишних действий">
  <meta property="og:description" content="Сделайте снимок, добавьте пометки и получите готовую ссылку за несколько секунд.">
  <meta property="og:image" content="https://s.bu5inessman.ru/assets/og.png">
  <meta property="og:image:width" content="1200">
  <meta property="og:image:height" content="630">
  <meta name="twitter:card" content="summary_large_image">
  <title>B5MShot — скриншоты без лишних действий</title>
  <style>
    :root{--bg:#11151f;--surface:#202738;--surface2:#293247;--line:#3b4760;--text:#f7f8fc;--muted:#aeb8ca;--violet:#8b62ff;--blue:#48a7ff;--green:#5cd39b;--shadow:0 28px 90px rgba(4,7,14,.48)}
    *{box-sizing:border-box}
    html{scroll-behavior:smooth}
    body{margin:0;min-width:320px;background:radial-gradient(circle at 16% 5%,rgba(128,96,255,.17),transparent 28%),radial-gradient(circle at 85% 20%,rgba(66,161,255,.13),transparent 26%),var(--bg);color:var(--text);font-family:"Segoe UI Variable Text","Segoe UI",system-ui,sans-serif;line-height:1.55}
    body:before{content:"";position:fixed;inset:0;pointer-events:none;opacity:.18;background-image:linear-gradient(rgba(255,255,255,.035) 1px,transparent 1px),linear-gradient(90deg,rgba(255,255,255,.035) 1px,transparent 1px);background-size:52px 52px;mask-image:linear-gradient(to bottom,black,transparent 78%)}
    a{color:inherit;text-decoration:none}
    .container{width:min(1160px,calc(100% - 40px));margin:auto;position:relative}
    .nav{height:82px;display:flex;align-items:center;justify-content:space-between}
    .brand{display:flex;align-items:center;gap:12px;font-size:20px;font-weight:760;letter-spacing:-.02em}
    .brand img{width:42px;height:42px;padding:4px;border-radius:13px;background:#2d3750;border:1px solid #4b5874;box-shadow:0 9px 28px rgba(0,0,0,.28)}
    .nav-actions{display:flex;align-items:center;gap:14px;color:var(--muted);font-size:14px}
    .nav-link:hover{color:white}
    .button{display:inline-flex;align-items:center;justify-content:center;gap:10px;min-height:48px;padding:0 20px;border-radius:12px;border:1px solid var(--line);background:var(--surface2);font-weight:680;transition:transform .18s ease,border-color .18s ease,filter .18s ease}
    .button:hover{transform:translateY(-2px);border-color:#687793;filter:brightness(1.08)}
    .button:focus-visible{outline:3px solid rgba(83,167,255,.45);outline-offset:3px}
    .button.primary{border:0;background:linear-gradient(135deg,var(--violet),#596cff 58%,var(--blue));box-shadow:0 14px 36px rgba(91,103,255,.28)}
    .button.compact{min-height:40px;padding:0 16px;font-size:13px}
    .hero{display:grid;grid-template-columns:minmax(0,1.02fr) minmax(420px,.98fr);align-items:center;gap:64px;padding:82px 0 98px}
    .eyebrow{display:inline-flex;align-items:center;gap:8px;padding:7px 11px;border-radius:999px;background:rgba(92,211,155,.09);border:1px solid rgba(92,211,155,.25);color:#82e3b8;font-size:12px;font-weight:700;text-transform:uppercase;letter-spacing:.08em}
    .dot{width:7px;height:7px;border-radius:50%;background:var(--green);box-shadow:0 0 14px var(--green)}
    h1{margin:22px 0 20px;font-size:clamp(46px,5.8vw,76px);line-height:1.02;letter-spacing:-.055em;max-width:760px}
    .gradient-text{background:linear-gradient(100deg,#aa8cff 8%,#6c87ff 51%,#59beff);-webkit-background-clip:text;background-clip:text;color:transparent}
    .lead{max-width:640px;margin:0;color:#bdc6d6;font-size:clamp(17px,1.8vw,20px)}
    .hero-actions{display:flex;flex-wrap:wrap;gap:12px;margin-top:32px}
    .download-note{display:flex;align-items:center;gap:10px;margin-top:16px;color:#7f8da5;font-size:12px}
    .windows-mark{display:inline-grid;grid-template-columns:repeat(2,5px);gap:2px;transform:skewY(-5deg)}
    .windows-mark i{width:5px;height:5px;background:#70bbff}
    .app-card{position:relative;padding:14px;border-radius:28px;background:linear-gradient(145deg,#2a3244,#1a202d);border:1px solid #46536b;box-shadow:var(--shadow);transform:rotate(1deg)}
    .app-card:before{content:"";position:absolute;inset:-1px;border-radius:28px;padding:1px;background:linear-gradient(135deg,rgba(152,115,255,.7),transparent 35%,rgba(73,173,255,.5));mask:linear-gradient(#000 0 0) content-box,linear-gradient(#000 0 0);mask-composite:exclude;pointer-events:none}
    .app-inner{padding:20px;border-radius:19px;background:linear-gradient(150deg,#252c3c,#1d2331)}
    .app-head{display:flex;align-items:center;gap:11px;margin-bottom:20px}
    .app-head img{width:38px;height:38px;padding:3px;border-radius:11px;background:#34405a}
    .app-title{font-weight:750}.app-sub{font-size:11px;color:var(--muted)}
    .status-dot{display:inline-block;width:6px;height:6px;margin-right:5px;border-radius:50%;background:var(--green)}
    .capture{display:flex;align-items:center;justify-content:space-between;padding:17px;border-radius:13px;background:linear-gradient(115deg,#8d62ff,#4a9fff);font-weight:700}
    .key{padding:5px 8px;border-radius:7px;background:rgba(23,31,54,.32);font-size:10px;font-weight:600}
    .screen-action{display:flex;justify-content:space-between;margin-top:10px;padding:13px;border:1px solid #3a455b;border-radius:11px;background:#293247;color:#d8deea;font-size:13px}
    .mock-panel{margin-top:14px;padding:15px;border:1px solid #3b465b;border-radius:14px;background:#202736}
    .mock-panel-title{display:flex;justify-content:space-between;font-size:13px;font-weight:700}.mock-panel-title span:last-child{color:#8290a7;font-size:10px;font-weight:500}
    .shortcut{display:flex;justify-content:space-between;align-items:center;margin-top:11px;color:#aeb8ca;font-size:11px}.shortcut b{padding:7px 10px;border-radius:8px;background:#303a50;color:white;font-size:11px}
    .trust-row{display:grid;grid-template-columns:repeat(3,1fr);gap:12px;padding-bottom:74px}
    .trust{padding:19px 20px;border:1px solid rgba(73,86,112,.75);border-radius:16px;background:rgba(32,39,56,.66);backdrop-filter:blur(10px)}
    .trust strong{display:block;font-size:20px}.trust span{color:var(--muted);font-size:13px}
    .section{padding:86px 0}
    .section-head{max-width:680px;margin-bottom:38px}.kicker{color:#8bbfff;font-size:12px;font-weight:750;text-transform:uppercase;letter-spacing:.1em}
    h2{margin:9px 0 12px;font-size:clamp(32px,4vw,48px);line-height:1.08;letter-spacing:-.035em}.section-head p{margin:0;color:var(--muted);font-size:17px}
    .features{display:grid;grid-template-columns:repeat(3,1fr);gap:16px}
    .feature{min-height:190px;padding:24px;border:1px solid #364158;border-radius:18px;background:linear-gradient(145deg,rgba(39,47,65,.9),rgba(27,33,47,.9));transition:transform .18s ease,border-color .18s ease}
    .feature:hover{transform:translateY(-4px);border-color:#586884}.feature-icon{display:grid;place-items:center;width:42px;height:42px;border-radius:12px;background:linear-gradient(135deg,rgba(139,98,255,.24),rgba(72,167,255,.18));border:1px solid rgba(128,115,255,.28);font-size:20px}
    .feature h3{margin:17px 0 7px;font-size:17px}.feature p{margin:0;color:var(--muted);font-size:14px}
    .steps{display:grid;grid-template-columns:repeat(3,1fr);gap:18px;counter-reset:steps}
    .step{position:relative;padding:25px 24px 25px 74px;border-top:1px solid #46536b;background:linear-gradient(180deg,rgba(41,50,71,.42),transparent);counter-increment:steps}
    .step:before{content:counter(steps);position:absolute;left:20px;top:22px;display:grid;place-items:center;width:36px;height:36px;border-radius:11px;background:linear-gradient(135deg,var(--violet),var(--blue));font-weight:800}.step h3{margin:0 0 6px}.step p{margin:0;color:var(--muted);font-size:14px}
    .cta{display:grid;grid-template-columns:1fr auto;align-items:center;gap:30px;margin:72px 0 86px;padding:42px;border:1px solid #4a5772;border-radius:24px;background:radial-gradient(circle at 90% 20%,rgba(76,166,255,.19),transparent 32%),linear-gradient(135deg,#282f43,#1d2432);box-shadow:var(--shadow)}
    .cta h2{margin:0 0 9px;font-size:36px}.cta p{margin:0;color:var(--muted)}
    footer{display:flex;justify-content:space-between;align-items:center;padding:28px 0 38px;border-top:1px solid #293347;color:#7f8ba0;font-size:12px}.footer-links{display:flex;gap:18px}.footer-links a:hover{color:white}
    @media(max-width:900px){.hero{grid-template-columns:1fr;padding-top:48px}.app-card{max-width:610px;transform:none}.features{grid-template-columns:repeat(2,1fr)}.steps{grid-template-columns:1fr}.cta{grid-template-columns:1fr}.trust-row{grid-template-columns:1fr}}
    @media(max-width:600px){.container{width:min(100% - 28px,1160px)}.nav{height:70px}.nav-link{display:none}.hero{gap:44px;padding:42px 0 64px}.hero-actions .button{width:100%}.app-card{padding:8px;border-radius:22px}.app-inner{padding:15px}.features{grid-template-columns:1fr}.section{padding:64px 0}.cta{padding:28px 22px;margin:52px 0 64px}.cta h2{font-size:30px}footer{align-items:flex-start;gap:18px;flex-direction:column}}
    @media(prefers-reduced-motion:reduce){html{scroll-behavior:auto}.button,.feature{transition:none}}
  </style>
</head>
<body>
  <header class="container nav">
    <a class="brand" href="/" aria-label="B5MShot — главная"><img src="/assets/logo.png" alt=""><span>B5MShot</span></a>
    <nav class="nav-actions" aria-label="Основная навигация"><a class="nav-link" href="#features">Возможности</a><a class="nav-link" href="#how">Как работает</a><a class="button compact" href="/download/B5MShot.exe">Скачать</a></nav>
  </header>
  <main>
    <section class="container hero">
      <div>
        <div class="eyebrow"><span class="dot"></span> Создано для Windows</div>
        <h1>Скриншот. Правки. <span class="gradient-text">Готовая ссылка.</span></h1>
        <p class="lead">B5MShot помогает выделить область экрана, сразу добавить стрелки, текст или мозаику и загрузить результат в облако — без лишних окон и аккаунтов.</p>
        <div class="hero-actions"><a class="button primary" href="/download/B5MShot.exe">Скачать B5MShot 0.5.0 <span aria-hidden="true">↓</span></a><a class="button" href="https://t.me/BU5INESSMAN" rel="noreferrer">Связаться с разработчиком</a></div>
        <div class="download-note"><span class="windows-mark" aria-hidden="true"><i></i><i></i><i></i><i></i></span> Windows 10/11 · x64 · 68,7 МБ · бесплатная загрузка</div>
      </div>
      <div class="app-card" aria-label="Интерфейс B5MShot">
        <div class="app-inner">
          <div class="app-head"><img src="/assets/logo.png" alt=""><div><div class="app-title">B5MShot</div><div class="app-sub"><span class="status-dot"></span>Работает в фоне</div></div></div>
          <div class="capture"><span>⌗ &nbsp; Выделить область</span><span class="key">Print Screen</span></div>
          <div class="screen-action"><span>Снимок всего экрана</span><span>Ctrl + Print Screen</span></div>
          <div class="mock-panel"><div class="mock-panel-title"><span>Горячие клавиши</span><span>любые сочетания</span></div><div class="shortcut"><span>Выделение области</span><b>Print Screen</b></div><div class="shortcut"><span>Весь экран</span><b>Ctrl + Print Screen</b></div></div>
        </div>
      </div>
    </section>

    <div class="container trust-row"><div class="trust"><strong>В системном трее</strong><span>Всегда рядом и не мешает работе</span></div><div class="trust"><strong>Своё облако</strong><span>Ссылки на домене s.bu5inessman.ru</span></div><div class="trust"><strong>До 15 МБ</strong><span>Быстрая загрузка PNG и JPEG</span></div></div>

    <section class="container section" id="features">
      <div class="section-head"><span class="kicker">Возможности</span><h2>Всё нужное сразу после снимка</h2><p>Инструменты собраны в одном редакторе и не требуют переключения между программами.</p></div>
      <div class="features">
        <article class="feature"><div class="feature-icon">⌗</div><h3>Область или весь экран</h3><p>Выделяйте нужный фрагмент или снимайте весь виртуальный рабочий стол.</p></article>
        <article class="feature"><div class="feature-icon">↗</div><h3>Редактор пометок</h3><p>Карандаш, линии, стрелки, рамки, текст и мозаика приватных данных.</p></article>
        <article class="feature"><div class="feature-icon">⌨</div><h3>Любые горячие клавиши</h3><p>Назначайте удобные сочетания для каждого режима прямо в приложении.</p></article>
        <article class="feature"><div class="feature-icon">☁</div><h3>Мгновенная ссылка</h3><p>Загрузите готовый снимок и сразу получите ссылку в буфере обмена.</p></article>
        <article class="feature"><div class="feature-icon">◉</div><h3>Работа в фоне</h3><p>Приложение живёт в трее и может автоматически запускаться вместе с Windows.</p></article>
        <article class="feature"><div class="feature-icon">↻</div><h3>Проверка обновлений</h3><p>B5MShot сообщит о новой версии и покажет список изменений перед скачиванием.</p></article>
      </div>
    </section>

    <section class="container section" id="how">
      <div class="section-head"><span class="kicker">Три шага</span><h2>От экрана до ссылки за секунды</h2></div>
      <div class="steps"><article class="step"><h3>Сделайте снимок</h3><p>Нажмите Print Screen и выделите нужную область.</p></article><article class="step"><h3>Добавьте пометки</h3><p>Подчеркните главное, добавьте текст или скройте приватные данные.</p></article><article class="step"><h3>Поделитесь</h3><p>Нажмите «Загрузить» — ссылка автоматически скопируется.</p></article></div>
    </section>

    <section class="container cta"><div><h2>Готовы сделать первый снимок?</h2><p>Скачайте B5MShot для Windows и запустите — установка .NET не требуется.</p></div><a class="button primary" href="/download/B5MShot.exe">Скачать для Windows <span aria-hidden="true">↓</span></a></section>
  </main>
  <footer class="container"><span>© 2026 B5MShot · Снимки по ссылке являются публичными</span><div class="footer-links"><a href="/health">Состояние сервиса</a><a href="https://t.me/BU5INESSMAN" rel="noreferrer">Сообщить об ошибке</a></div></footer>
</body>
</html>
""";
}
