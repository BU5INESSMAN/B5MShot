(() => {
 'use strict';
 const reduced = matchMedia('(prefers-reduced-motion: reduce)');
 const image = document.querySelector('#editorDemo');
 const button = document.querySelector('#playDemo');
 const label = document.querySelector('#playLabel');
 let playing = false;
 let observer;
 const stop = () => {
  playing = false;
  image.src = '/assets/editor-poster.webp?v=3';
  button.setAttribute('aria-pressed','false'); label.textContent = 'Смотреть анимацию';
  button.querySelector('.play-symbol').textContent = '▶';
 };
 button.addEventListener('click', () => {
  if (playing) { stop(); return; }
  playing = true; image.src = '/assets/editor-demo.webp?v=3';
  button.setAttribute('aria-pressed','true'); label.textContent = 'Пауза';
  button.querySelector('.play-symbol').textContent = 'Ⅱ';
 });
 image.addEventListener('error', () => { if (playing) { stop(); label.textContent = 'Повторить загрузку'; } });
 document.addEventListener('visibilitychange', () => { if (document.hidden && playing) stop(); });
 reduced.addEventListener('change', () => { if (reduced.matches) { stop(); observer?.disconnect(); document.querySelectorAll('.reveal').forEach(el=>el.classList.add('visible')); } });
 if (!reduced.matches && 'IntersectionObserver' in window) {
  document.documentElement.classList.add('motion-ready');
  observer = new IntersectionObserver(entries => entries.forEach(entry => { if(entry.isIntersecting){entry.target.classList.add('visible');observer.unobserve(entry.target);} }), {threshold:.08});
  document.querySelectorAll('.reveal').forEach(el=>observer.observe(el));
 }
})();
