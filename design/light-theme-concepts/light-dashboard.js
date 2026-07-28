const concepts = [
  ['05-morning-meadow.html', 'Утренний луг', '#2C8E7E'],
  ['06-warm-paper.html', 'Тёплая бумага', '#A96234'],
  ['07-ice-river.html', 'Ледяная река', '#2878B9'],
  ['08-berry-notes.html', 'Ягодные заметки', '#8A4F8C'],
  ['09-sage-clinic.html', 'Шалфейная клиника', '#4D7B68'],
  ['10-coral-dawn.html', 'Коралловый рассвет', '#C9615E']
];

const current = document.documentElement.dataset.concept;
const concept = concepts.find(([file]) => file === current) ?? concepts[0];
document.title = `SugarGuard — ${concept[1]}`;

const nav = ['Обзор', 'Измерения', 'Уведомления', 'Рюкзак', 'Дневник питания', 'Контроль ИИ', 'Профиль ребёнка'];
const tabs = ['Обзор', 'Измерения', 'Уведомления', 'Рюкзак', 'Дневник питания', 'Контроль ИИ', 'Записки врача', 'Профиль ребёнка', 'Статьи', 'Поддержка'];

document.body.innerHTML = `
  <div class="shell">
    <aside class="sidebar">
      <div class="brand"><div class="brand-mark">SG</div><div><b>SugarGuard</b><span>Кабинет родителя</span></div></div>
      <div class="person-card"><div class="avatar">ТП</div><div><b>Тимофей Петров</b><small>13 лет</small></div><span class="value-pill">6,2</span></div>
      <div><p class="nav-title">Навигация</p><nav class="nav">${nav.map((item, index) => `<a class="${index === 0 ? 'active' : ''}" href="#">${item}</a>`).join('')}</nav></div>
      <div class="sidebar-footer"><div class="avatar">Я</div><span>Родитель</span></div>
    </aside>
    <main class="main">
      <header class="topbar"><div><h1>Главная</h1><p>Обзор состояния ребёнка</p></div><button class="bell" aria-label="Уведомления">◌</button></header>
      <section class="top-context"><div class="child"><div class="avatar">ТП</div><div><b>Тимофей Петров</b><span>Диабет 1 типа · 13 лет</span></div></div><div class="current">6,2 ммоль/л</div></section>
      <nav class="tabbar">${tabs.map((item, index) => `<a class="${index === 0 ? 'active' : ''}" href="#">${item}</a>`).join('')}</nav>
      <section class="content">
        <div class="swatches">${concepts.map(([file, name, color]) => `<a class="swatch ${file === current ? 'active' : ''}" style="--swatch:${color}" href="${file}">${name}</a>`).join('')}</div>
        <div class="concept-note"><b>${concept[1]}</b><span>Светлая палитра для SugarGuard: спокойная, медицински понятная и без стерильного «офисного» ощущения.</span></div>
        <div class="dashboard-heading"><div><div class="eyebrow">Состояние сегодня</div><h2>Всё важное — спокойно и рядом</h2></div><div class="updated"><strong>●</strong> Синхронизация 2 минуты назад</div></div>
        <section class="metric-grid">
          <article class="metric primary"><div class="metric-label">Глюкоза сейчас</div><div class="metric-number">6,2 <small>ммоль/л</small></div><p>В целевом диапазоне</p></article>
          <article class="metric"><div class="metric-label">Время в диапазоне</div><div class="metric-number">78<small>%</small></div><p>За последние 7 дней</p></article>
          <article class="metric critical"><div class="metric-label">Ниже нормы</div><div class="metric-number">4<small>%</small></div><p>1 эпизод за неделю</p></article>
          <article class="metric warn"><div class="metric-label">Выше нормы</div><div class="metric-number">18<small>%</small></div><p>Контроль после ужина</p></article>
        </section>
        <section class="dashboard-grid">
          <article class="panel"><div class="panel-head"><div><div class="eyebrow">Глюкоза</div><h3>Динамика за 24 часа</h3><p>Целевой диапазон 4,0–10,0 ммоль/л</p></div><div class="periods"><span>6ч</span><span>12ч</span><span class="selected">24ч</span></div></div><div class="chart"><div class="target-range"></div><svg class="trend" viewBox="0 0 800 280" preserveAspectRatio="none"><path class="area" d="M0 168 L80 150 L160 164 L240 132 L320 150 L400 119 L480 128 L560 104 L640 128 L720 116 L800 127 L800 280 L0 280Z"/><line x1="0" y1="112" x2="800" y2="112"/><polyline points="0,168 80,150 160,164 240,132 320,150 400,119 480,128 560,104 640,128 720,116 800,127"/><circle cx="0" cy="168" r="5"/><circle cx="400" cy="119" r="5"/><circle cx="800" cy="127" r="6"/></svg></div><div class="legend"><span><i class="good"></i>В диапазоне</span><span><i></i>Текущее значение</span><span><i class="bad"></i>Критическое</span></div></article>
          <article class="panel"><div class="panel-head"><div><div class="eyebrow">Лента</div><h3>Недавние события</h3></div></div><div class="events"><div class="event"><div class="event-icon">●</div><div><b>Новое измерение</b><span>6,2 ммоль/л · перед перекусом</span></div><time>2 мин</time></div><div class="event"><div class="event-icon">↻</div><div><b>Синхронизация завершена</b><span>Данные телефона сохранены</span></div><time>9 мин</time></div><div class="event critical"><div class="event-icon">!</div><div><b>Низкое значение ночью</b><span>3,8 ммоль/л · уведомление прочитано</span></div><time>7:12</time></div><div class="event"><div class="event-icon">▣</div><div><b>Завтрак добавлен</b><span>4,2 ХЕ · 4 ед. инсулина</span></div><time>7:35</time></div></div></article>
        </section>
        <section class="bottom-grid"><article class="panel mini"><h3>Цель на сегодня</h3><div class="mini-row"><span>Измерений</span><b>5 из 6</b></div><div class="bar"><span style="width:83%"></span></div></article><article class="panel mini"><h3>Питание</h3><div class="mini-row"><span>Съедено</span><b>10,6 ХЕ</b></div><div class="mini-row"><span>Следующий приём</span><b>Обед · 12:30</b></div></article><article class="panel mini"><h3>На что обратить внимание</h3><div class="mini-row"><span class="rose">Ночное значение</span><b>3,8 ммоль/л</b></div><div class="mini-row"><span>Контекст сохранён</span><b>✓</b></div></article></section>
      </section>
    </main>
  </div>`;
