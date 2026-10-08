const state = { user: null, movies: [], catalog: [], library: [], lists: [], selectedMovie: null };
const $ = id => document.getElementById(id);
const esc = v => String(v ?? '').replace(/[&<>"']/g, c => ({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
const validUrl = value => { try { const u = new URL(value); return ['https:','http:'].includes(u.protocol) ? u.href : ''; } catch { return ''; } };
const img = m => validUrl(m.imageUrl) || '/poster-placeholder.svg';
function toast(message) { const el = $('toast'); el.textContent = message; el.hidden = false; clearTimeout(toast.timer); toast.timer = setTimeout(() => el.hidden = true, 3500); }
async function api(path, options = {}) {
  const response = await fetch('/api' + path, { credentials:'same-origin', ...options, headers:{'Content-Type':'application/json','X-Requested-With':'XMLHttpRequest',...(options.headers || {})} });
  const data = await response.json().catch(() => ({}));
  if (!response.ok) throw new Error(data.error || (response.status === 401 ? 'Bitte melde dich an.' : 'Aktion fehlgeschlagen.'));
  return data;
}
function openDialog(content) { $('dialogBody').innerHTML = content; $('dialog').hidden = false; }
function closeDialog() { $('dialog').hidden = true; $('dialogBody').innerHTML = ''; }
function requireLogin() { if (state.user) return true; openAuth('login'); return false; }
function updateAccount() {
  $('userBadge').textContent = state.user?.username || '';
  $('loginBtn').hidden = !!state.user; $('logoutBtn').hidden = !state.user;
  $('adminNav').hidden = !state.user?.isAdmin;
  if (state.user) { $('profileForm').elements.username.value = state.user.username; $('profileForm').elements.genres.value = (state.user.favoriteGenres || []).join(', '); }
}
async function refreshUser() { const data = await api('/me'); state.user = data.user; updateAccount(); if (state.user) state.library = await api('/library'); else state.library = []; }
function showView(name) {
  if (!$(name) || name === 'admin' && !state.user?.isAdmin) name = 'entdecken';
  document.querySelectorAll('.view').forEach(v => v.classList.toggle('active', v.id === name));
  document.querySelectorAll('nav [data-view]').forEach(b => b.classList.toggle('active', b.dataset.view === name));
  location.hash = name;
  if (name === 'watchlist') renderLibrary();
  if (name === 'empfehlungen') loadRecommendations();
  if (name === 'listen') loadLists();
  if (name === 'admin') loadAdmin();
}
function movieCard(m, reason = '') {
  const library = state.library.find(x => x.movieId === m.id) || {};
  const ratingLabel = m.reviewCount ? `${m.reviewCount} Bewertungen` : (m.source === 'tmdb' ? 'TMDB-Wertung' : 'Noch unbewertet');
  return `<article class="card"><img class="poster" src="${esc(img(m))}" alt="Poster: ${esc(m.title)}" loading="lazy"><div class="card-body"><h3>${esc(m.title)}</h3><div class="meta">${m.year} · <span class="rating">★ ${Number(m.displayRating || 0).toFixed(1)}</span> · ${ratingLabel}</div>${m.source === 'tmdb' ? '<span class="source-badge">Daten: TMDB</span>' : ''}${reason ? `<div class="reason">${esc(reason)}</div>` : ''}<div class="pills">${(m.genres || []).slice(0,3).map(g => `<span class="pill">${esc(g)}</span>`).join('')}</div><div class="card-actions"><button class="secondary" data-action="detail" data-id="${m.id}">Details</button><button class="secondary" data-action="watchlist" data-id="${m.id}" aria-label="Watchlist">${library.watchlist ? '✓ Merkliste' : '+ Merkliste'}</button><button class="secondary" data-action="seen" data-id="${m.id}">${library.seen ? '✓ Gesehen' : 'Gesehen'}</button></div></div></article>`;
}
async function loadMovies() {
  const params = new URLSearchParams();
  for (const id of ['genre','actor','fromYear','toYear','rating','sort']) {
    const value = $(id).value.trim(); if (value) params.set(id === 'rating' ? 'minRating' : id, value);
  }
  if ($('search').value.trim()) params.set('query', $('search').value.trim());
  state.movies = await api('/movies?' + params.toString());
  $('movieGrid').innerHTML = state.movies.length ? state.movies.map(m => movieCard(m)).join('') : '<p class="empty">Keine Filme gefunden. Ändere die Filter.</p>';
  $('resultCount').textContent = `${state.movies.length} Filme`;
  const all = await api('/movies');
  state.catalog = all;
  const current = $('genre').value;
  const genres = [...new Set(all.flatMap(m => m.genres || []))].sort();
  $('genre').innerHTML = '<option value="">Alle Genres</option>' + genres.map(g => `<option value="${esc(g)}">${esc(g)}</option>`).join('');
  $('genre').value = current;
}
function renderLibrary() {
  const watch = state.catalog.filter(m => state.library.some(x => x.movieId === m.id && x.watchlist));
  const seen = state.catalog.filter(m => state.library.some(x => x.movieId === m.id && x.seen));
  $('watchGrid').innerHTML = watch.length ? watch.map(m => movieCard(m)).join('') : '<p class="empty">Deine Watchlist ist noch leer.</p>';
  $('seenGrid').innerHTML = seen.length ? seen.map(m => movieCard(m)).join('') : '<p class="empty">Noch keine Filme als gesehen markiert.</p>';
}
async function loadRecommendations() {
  if (!requireLogin()) return;
  const items = await api('/recommendations');
  $('recommendGrid').innerHTML = items.length ? items.map(x => movieCard(x.movie, x.reason)).join('') : '<p class="empty">Bewerte Filme oder wähle Lieblingsgenres in deinem Profil.</p>';
}
async function showMovie(id) {
  const m = await api('/movies/' + id); state.selectedMovie = m;
  const reviews = (m.reviews || []).map(r => `<div class="review"><strong>${esc(r.username)}</strong> <span class="rating">★ ${r.score}/10</span><p>${esc(r.comment)}</p><div class="review-actions"><button class="secondary" data-action="helpful" data-id="${r.id}">Hilfreich (${r.helpfulCount})</button><button class="secondary" data-action="comment" data-id="${r.id}">Kommentieren</button></div><div class="review-comments">${(r.comments || []).map(c => `<p><b>${esc(c.username)}:</b> ${esc(c.text)}</p>`).join('')}</div></div>`).join('');
  openDialog(`<div class="detail-top"><img src="${esc(img(m))}" alt="Poster"><div><span class="eyebrow">FILMDETAILS</span><h2>${esc(m.title)}</h2><p>${m.releaseDate || m.year} · ${esc((m.genres || []).join(', '))}</p><p class="rating">★ ${Number(m.displayRating).toFixed(1)} / 10 ${m.reviewCount ? 'Community' : (m.source === 'tmdb' ? 'TMDB' : '')}</p>${m.source === 'tmdb' ? '<p class="source-badge">Filmdaten und Poster von TMDB</p>' : ''}<p>${esc(m.plot)}</p><p><b>Regie:</b> ${esc(m.director)}<br><b>Besetzung:</b> ${esc((m.cast || []).join(', '))}</p><button class="secondary" data-action="watchlist" data-id="${m.id}">Watchlist umschalten</button> <button class="secondary" data-action="seen" data-id="${m.id}">Gesehen umschalten</button> <button class="secondary" data-action="addToList" data-id="${m.id}">Zu Liste</button></div></div><h3>Rezensionen</h3>${reviews || '<p>Noch keine Rezensionen.</p>'}<form id="ratingForm"><h3>Deine Bewertung</h3><label>Punkte von 1 bis 10<input name="score" type="number" min="1" max="10" required></label><label>Rezension<textarea name="text" maxlength="2000" rows="3"></textarea></label><button class="primary">Bewerten</button></form>`);
  $('ratingForm').onsubmit = async e => { e.preventDefault(); if (!requireLogin()) return; try { await api('/rating', {method:'POST',body:JSON.stringify({movieId:m.id,score:Number(e.target.elements.score.value),reviewText:e.target.elements.text.value})}); toast('Bewertung gespeichert'); await loadMovies(); await showMovie(m.id); } catch(err) { toast(err.message); } };
}
async function toggleLibrary(kind, movieId) {
  if (!requireLogin()) return;
  const entry = state.library.find(x => x.movieId === movieId);
  const value = !(entry?.[kind]);
  await api('/library/' + kind, {method:'PUT',body:JSON.stringify({movieId,value})});
  state.library = await api('/library');
  await loadMovies();
  if (document.querySelector('#watchlist.active')) renderLibrary();
  toast(value ? 'Gespeichert' : 'Entfernt');
}
async function loadLists() {
  state.lists = await api('/lists');
  $('listGrid').innerHTML = state.lists.length ? state.lists.map(l => `<div class="list-card"><h3>${esc(l.name)}</h3><small>von ${esc(l.owner)} · ${l.isPublic ? 'öffentlich' : 'privat'}</small>${l.userId === state.user?.id ? `<p><button class=\"secondary\" data-action=\"invite\" data-id=\"${l.id}\">Mitwirkende einladen</button></p>` : ''}<ul>${(l.movieIds || []).map(id => `<li>${esc(state.catalog.find(m => m.id === id)?.title || 'Film')}</li>`).join('')}</ul></div>`).join('') : '<p class="empty">Noch keine Listen vorhanden.</p>';
}
function openAuth(mode) {
  const register = mode === 'register';
  openDialog(`<h2>${register ? 'Konto erstellen' : 'Anmelden'}</h2><form id="authForm"><label>Benutzername<input name="username" required maxlength="40" autocomplete="username"></label><label>Passwort<input name="password" type="password" required ${register ? 'minlength="8" autocomplete="new-password"' : 'autocomplete="current-password"'}></label>${register ? '<label>Lieblingsgenres<input name="genres" placeholder="Drama, Sci-Fi"></label>' : ''}<button class="primary">${register ? 'Registrieren' : 'Anmelden'}</button></form><p>${register ? 'Schon ein Konto?' : 'Noch kein Konto?'} <button class="secondary" data-action="switchAuth" data-mode="${register ? 'login' : 'register'}">${register ? 'Anmelden' : 'Registrieren'}</button></p>`);
  $('authForm').onsubmit = async e => { e.preventDefault(); const f = e.target.elements; const body = {username:f.username.value,password:f.password.value}; if (register) body.favoriteGenres = f.genres.value.split(',').map(x => x.trim()).filter(Boolean); try { await api(register ? '/register':'/login', {method:'POST',body:JSON.stringify(body)}); await refreshUser(); closeDialog(); await loadMovies(); toast('Willkommen bei IMJK'); } catch(err) { toast(err.message); } };
}
function openMovieForm() {
  if (!requireLogin()) return;
  openDialog('<h2>Film vorschlagen</h2><p>Dein Vorschlag wird vor der Veröffentlichung geprüft.</p><form id="movieForm"><label>Titel<input name="title" required maxlength="160"></label><label>Jahr<input name="year" type="number" required min="1888" max="2100"></label><label>Poster URL<input name="imageUrl" type="url"></label><label>Genres, mit Komma getrennt<input name="genres"></label><label>Besetzung, mit Komma getrennt<input name="cast"></label><label>Regie<input name="director"></label><label>Beschreibung<textarea name="description" required maxlength="4000"></textarea></label><button class="primary">Einreichen</button></form>');
  $('movieForm').onsubmit = async e => { e.preventDefault(); const f = e.target.elements; const payload = {title:f.title.value,year:Number(f.year.value),imageUrl:f.imageUrl.value,genres:f.genres.value.split(',').map(x=>x.trim()),cast:f.cast.value.split(',').map(x=>x.trim()),director:f.director.value,description:f.description.value}; try { const result = await api('/movies',{method:'POST',body:JSON.stringify(payload)}); closeDialog(); toast(result.pending ? 'Film zur Prüfung eingereicht' : 'Film veröffentlicht'); await loadMovies(); } catch(err) { toast(err.message); } };
}
function openListForm() {
  if (!requireLogin()) return;
  openDialog('<h2>Neue Filmliste</h2><form id="listForm"><label>Name<input name="name" required maxlength="80"></label><label><input name="public" type="checkbox" checked> Öffentlich teilen</label><button class="primary">Liste anlegen</button></form>');
  $('listForm').onsubmit = async e => { e.preventDefault(); try { await api('/lists',{method:'POST',body:JSON.stringify({name:e.target.elements.name.value,isPublic:e.target.elements.public.checked})}); closeDialog(); await loadLists(); toast('Liste erstellt'); } catch(err) { toast(err.message); } };
}
async function addToList(movieId) {
  if (!requireLogin()) return;
  const lists = (await api('/lists')).filter(l => l.userId === state.user.id || (l.contributorIds || []).includes(state.user.id));
  if (!lists.length) { openListForm(); return; }
  openDialog('<h2>Zu Liste hinzufügen</h2>' + lists.map(l => `<p><button class="secondary" data-action="saveListItem" data-id="${l.id}" data-movie="${movieId}">${esc(l.name)}</button></p>`).join(''));
}
async function loadAdmin() {
  if (!state.user?.isAdmin) return;
  const data = await api('/admin/queue');
  $('adminMovies').innerHTML = data.movies.length ? data.movies.map(m => `<div class="queue-row"><span>${esc(m.title)} (${m.year})</span><span><button class="primary" data-action="approve" data-id="${m.id}">Freigeben</button><button class="danger" data-action="reject" data-id="${m.id}">Ablehnen</button></span></div>`).join('') : '<p>Keine offenen Filmvorschläge.</p>';
  $('adminReviews').innerHTML = data.reviews.length ? data.reviews.map(r => `<div class="queue-row"><span><b>${esc(r.username)}</b> ★ ${r.score}: ${esc(r.comment)}</span><button class="danger" data-action="deleteReview" data-id="${r.id}">Entfernen</button></div>`).join('') : '<p>Keine Rezensionen.</p>';
}
const debounce = (fn, delay) => { let timer; return () => { clearTimeout(timer); timer = setTimeout(fn, delay); }; };
async function init() {
  try {
    const [status] = await Promise.all([api('/tmdb/status'), refreshUser()]);
    $('tmdbSyncStatus').textContent = status.configured
      ? (status.lastSuccessfulSync ? `TMDB zuletzt aktualisiert: ${new Date(status.lastSuccessfulSync).toLocaleString('de-DE')}` : 'TMDB-Import ist eingerichtet.')
      : 'TMDB-Import wartet auf Zugangsdaten.';
    await loadMovies();
    showView(location.hash.slice(1) || 'entdecken');
  } catch(err) { toast(err.message); }
  document.querySelectorAll('nav [data-view]').forEach(b => b.onclick = () => showView(b.dataset.view));
  $('loginBtn').onclick = () => openAuth('login');
  $('logoutBtn').onclick = async () => { try { await api('/logout',{method:'POST'}); await refreshUser(); showView('entdecken'); await loadMovies(); toast('Abgemeldet'); } catch(err) { toast(err.message); } };
  $('newMovieBtn').onclick = openMovieForm;
  $('createListBtn').onclick = openListForm;
  $('closeDialog').onclick = closeDialog;
  $('dialog').onclick = e => { if (e.target === $('dialog')) closeDialog(); };
  for (const id of ['genre','actor','fromYear','toYear','rating','sort']) $(id).addEventListener('change', () => loadMovies().catch(e => toast(e.message)));
  $('search').addEventListener('input', debounce(() => loadMovies().catch(e => toast(e.message)), 300));
  $('profileForm').onsubmit = async e => { e.preventDefault(); if (!requireLogin()) return; const f=e.target.elements; try { await api('/profile',{method:'PUT',body:JSON.stringify({newUsername:f.username.value,favoriteGenres:f.genres.value.split(',').map(x=>x.trim())})}); await refreshUser(); toast('Profil gespeichert'); } catch(err) { toast(err.message); } };
  document.addEventListener('click', async e => {
    const button = e.target.closest('[data-action]'); if (!button) return;
    const action = button.dataset.action, id = Number(button.dataset.id);
    try {
      if (action === 'detail') await showMovie(id);
      if (action === 'watchlist' || action === 'seen') await toggleLibrary(action,id);
      if (action === 'switchAuth') openAuth(button.dataset.mode);
      if (action === 'addToList') await addToList(id);
      if (action === 'invite') { const username = prompt('Benutzername der mitwirkenden Person:'); if (username?.trim()) { await api(`/lists/${id}/invite`, {method:'POST',body:JSON.stringify({username})}); toast('Einladung gespeichert'); } }
      if (action === 'saveListItem') { await api(`/lists/${id}/movies`,{method:'POST',body:JSON.stringify({movieId:Number(button.dataset.movie)})}); closeDialog(); toast('Zur Liste hinzugefügt'); }
      if (action === 'helpful') { if (!requireLogin()) return; await api(`/reviews/${id}/helpful`,{method:'POST'}); await showMovie(state.selectedMovie.id); }
      if (action === 'comment') { if (!requireLogin()) return; const text=prompt('Dein Kommentar:'); if (text?.trim()) { await api(`/reviews/${id}/comments`,{method:'POST',body:JSON.stringify({text})}); await showMovie(state.selectedMovie.id); } }
      if (['approve','reject','deleteReview'].includes(action)) { const path = action === 'deleteReview' ? `/admin/reviews/${id}` : `/admin/movies/${id}${action === 'approve' ? '/approve' : ''}`; await api(path,{method:action === 'approve'?'POST':'DELETE'}); await loadAdmin(); await loadMovies(); toast('Änderung gespeichert'); }
    } catch(err) { toast(err.message); }
  });
}
init();
