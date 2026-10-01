const storageKey = 'imjk-current-user';

const state = {
  currentUser: (() => {
    try {
      const current = localStorage.getItem(storageKey);
      return current ? JSON.parse(current) : null;
    } catch {
      return null;
    }
  })(),
  genre: '',
  query: '',
  sort: 'rating',
  reviewStars: 0,
  allMovies: []
};

const page = document.body.dataset.page || 'home';

const elements = {
  movieGrid: document.getElementById('movieGrid'),
  globalSearch: document.getElementById('globalSearch'),
  searchBtn: document.getElementById('searchBtn'),
  searchSuggestions: document.getElementById('searchSuggestions'),
  sortSelect: document.getElementById('sortSelect'),
  loginBtn: document.getElementById('loginBtn'),
  registerBtn: document.getElementById('registerBtn'),
  logoutBtn: document.getElementById('logoutBtn'),
  userBadge: document.getElementById('userBadge'),
  settingsBtn: document.getElementById('settingsBtn'),
  loginModal: document.getElementById('loginModal'),
  registerModal: document.getElementById('registerModal'),
  loginForm: document.getElementById('loginForm'),
  registerForm: document.getElementById('registerForm'),
  detailTitle: document.getElementById('detailTitle'),
  detailPoster: document.getElementById('detailPoster'),
  detailGenres: document.getElementById('detailGenres'),
  detailCast: document.getElementById('detailCast'),
  detailDirector: document.getElementById('detailDirector'),
  detailPlot: document.getElementById('detailPlot'),
  detailScore: document.getElementById('detailScore'),
  ratingForm: document.getElementById('ratingForm'),
  reviewList: document.getElementById('reviewList'),
  profileForm: document.getElementById('profileForm'),
  profileUsername: document.getElementById('profileUsername'),
  profileGenres: document.getElementById('profileGenres'),
  addMovieForm: document.getElementById('addMovieForm')
};

function saveCurrentUser() {
  if (state.currentUser) {
    localStorage.setItem(storageKey, JSON.stringify(state.currentUser));
  } else {
    localStorage.removeItem(storageKey);
  }
}

function updateUserUI() {
  if (!elements.userBadge) return;

  if (state.currentUser) {
    elements.userBadge.textContent = state.currentUser.username;
    elements.userBadge.classList.remove('hidden');
    if (elements.loginBtn) elements.loginBtn.classList.add('hidden');
    if (elements.registerBtn) elements.registerBtn.classList.add('hidden');
    if (elements.logoutBtn) elements.logoutBtn.classList.remove('hidden');
    if (elements.settingsBtn) elements.settingsBtn.classList.remove('hidden');
  } else {
    elements.userBadge.classList.add('hidden');
    if (elements.loginBtn) elements.loginBtn.classList.remove('hidden');
    if (elements.registerBtn) elements.registerBtn.classList.remove('hidden');
    if (elements.logoutBtn) elements.logoutBtn.classList.add('hidden');
    if (elements.settingsBtn) elements.settingsBtn.classList.remove('hidden');
  }
}

function toggleModal(modal, show) {
  if (!modal) return;
  modal.classList.toggle('hidden', !show);
}

function escapeHtml(value) {
  return String(value)
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;')
    .replace(/'/g, '&#039;');
}

function getSearchSuggestions(query) {
  const normalized = query.trim().toLowerCase();
  if (!normalized) return [];

  return [...state.allMovies]
    .map((movie) => {
      const title = String(movie.title || '').toLowerCase();
      let score = 0;

      if (title === normalized) score += 1000;
      if (title.startsWith(normalized)) score += 300;
      if (title.includes(normalized)) score += 200;

      const words = normalized.split(/\s+/).filter(Boolean);
      const matchedWords = words.filter((word) => title.includes(word)).length;
      score += matchedWords * 40;

      const maxLen = Math.max(title.length, normalized.length);
      const distancePenalty = maxLen === 0 ? 0 : (Math.abs(title.length - normalized.length) * 2);
      score += Math.max(0, 60 - distancePenalty);

      return { movie, score };
    })
    .filter((entry) => entry.score > 0)
    .sort((a, b) => b.score - a.score || a.movie.title.localeCompare(b.movie.title))
    .slice(0, 6)
    .map((entry) => entry.movie);
}

function renderSearchSuggestions() {
  if (!elements.searchSuggestions || !elements.globalSearch) return;

  const query = elements.globalSearch.value.trim();
  if (!query) {
    elements.searchSuggestions.classList.add('hidden');
    elements.searchSuggestions.innerHTML = '';
    return;
  }

  const suggestions = getSearchSuggestions(query);
  if (!suggestions.length) {
    elements.searchSuggestions.classList.add('hidden');
    elements.searchSuggestions.innerHTML = '';
    return;
  }

  elements.searchSuggestions.innerHTML = suggestions
    .map((movie) => `<button type="button" class="search-suggestion-item" data-title="${escapeHtml(movie.title)}">${escapeHtml(movie.title)}</button>`)
    .join('');

  elements.searchSuggestions.classList.remove('hidden');

  elements.searchSuggestions.querySelectorAll('.search-suggestion-item').forEach((button) => {
    button.addEventListener('click', () => {
      const chosenTitle = button.dataset.title;
      if (!chosenTitle) return;
      elements.globalSearch.value = chosenTitle;
      state.query = chosenTitle;
      elements.searchSuggestions.classList.add('hidden');
      fetchMovies();
    });
  });
}

async function fetchMovies() {
  const params = new URLSearchParams();
  if (state.genre) params.append('genre', state.genre);
  if (state.query) params.append('query', state.query);

  const response = await fetch(`/api/movies?${params.toString()}`);
  const movies = await response.json();
  state.allMovies = movies;

  const sorted = [...movies].sort((a, b) => {
    if (state.sort === 'year') return b.year - a.year;
    return Number(b.averageRating) - Number(a.averageRating);
  });

  renderMovies(sorted);
  startHeroCarousel(sorted);
}

let heroRotationTimer = null;

function getMoviePoster(movie) {
  return movie.imageUrl || 'https://m.media-amazon.com/images/G/01/imdb/images/social/imdb_logo.png';
}

function buildHeroCarousel() {
  const hero = document.querySelector('.hero');
  if (!hero || hero.dataset.carouselReady === 'true') return;

  hero.dataset.carouselReady = 'true';
  hero.innerHTML = `
    <div class="hero-slides">
      <div class="hero-slide active"></div>
      <div class="hero-slide"></div>
      <div class="hero-slide"></div>
      <div class="hero-slide"></div>
      <div class="hero-slide"></div>
    </div>
    <div class="hero-badges">
      <span class="tag tag-yellow">IMJK Katalog 2025</span>
      <span class="tag tag-light">Live Community</span>
    </div>
    <div class="hero-body">
      <div class="hero-copy">
        <h1 id="heroTitle">Willkommen bei IMJK</h1>
        <p id="heroText">Entdecke, bewerte und diskutiere Filme in einer Community mit Stil und Funktion.</p>
      </div>
      <div class="hero-score">
        <span class="star">★</span>
        <strong id="heroScore">9.8</strong>
      </div>
    </div>
  `;
}

function updateHeroCarousel(movies, currentIndex = 0) {
  const hero = document.querySelector('.hero');
  if (!hero) return;

  const slides = hero.querySelectorAll('.hero-slide');
  if (!slides.length || !movies.length) return;

  const visibleMovies = movies.slice(0, 5);

  slides.forEach((slide, index) => {
    const movie = visibleMovies[index] || visibleMovies[0];
    if (!movie) return;

    slide.style.backgroundImage = `linear-gradient(135deg, rgba(15,15,15,0.25), rgba(15,15,15,0.72)), url('${getMoviePoster(movie)}')`;
    slide.classList.toggle('active', index === currentIndex);
  });

  const activeMovie = visibleMovies[currentIndex] || visibleMovies[0];
  const heroTitle = document.getElementById('heroTitle');
  const heroText = document.getElementById('heroText');
  const heroScore = document.getElementById('heroScore');

  if (heroTitle) heroTitle.textContent = activeMovie.title;
  if (heroText) heroText.textContent = activeMovie.plot || 'Die größten Filme im aktuellen Katalog in Bewegung.';
  if (heroScore) heroScore.textContent = Number(activeMovie.averageRating || 0).toFixed(1);
}

function startHeroCarousel(movies) {
  const heroMovies = [...movies].slice(0, 5);
  if (!heroMovies.length) return;

  buildHeroCarousel();

  let currentIndex = 0;
  updateHeroCarousel(heroMovies, currentIndex);

  if (heroRotationTimer) clearInterval(heroRotationTimer);

  heroRotationTimer = setInterval(() => {
    currentIndex = (currentIndex + 1) % heroMovies.length;
    updateHeroCarousel(heroMovies, currentIndex);
  }, 3000);
}

function renderMovies(movies) {
  if (!elements.movieGrid) return;

  if (!movies.length) {
    elements.movieGrid.innerHTML = '<div class="movie-card"><div class="movie-body"><h3>Keine Treffer</h3><p>Versuche ein anderes Genre oder einen anderen Namen.</p></div></div>';
    return;
  }

  elements.movieGrid.innerHTML = movies.map((movie) => {
    const score = Number(movie.averageRating || 0).toFixed(1);
    const poster = getMoviePoster(movie);
    return `
      <article class="movie-card" data-title="${movie.title}">
        <div class="movie-poster" data-title="${movie.title}" data-score="★ ${score}" style="background: linear-gradient(135deg, rgba(0,0,0,0.2), rgba(12,12,12,0.7)), url('${poster}') center/cover no-repeat;"></div>
        <div class="movie-body">
          <div class="movie-meta">
            <span class="movie-year">${movie.year}</span>
            <span class="movie-score">${score}</span>
          </div>
          <h3>${movie.title}</h3>
          <p>${movie.plot}</p>
          <div class="genre-list">
            ${(movie.genres || []).slice(0, 4).map((genre) => `<span class="genre-pill">${genre}</span>`).join('')}
          </div>
          <div class="action-row">
            <button class="watch-btn">Trailer</button>
            <button class="rate-btn" data-title="${movie.title}">Bewerten</button>
          </div>
        </div>
      </article>
    `;
  }).join('');

  document.querySelectorAll('.movie-card').forEach((card) => {
    card.addEventListener('click', (event) => {
      const isActionButton = event.target.closest('.rate-btn') || event.target.closest('.watch-btn');
      if (isActionButton) return;
      const title = card.dataset.title;
      window.location.href = `/detail.html?movie=${encodeURIComponent(title)}`;
    });
  });

  document.querySelectorAll('.rate-btn').forEach((button) => {
    button.addEventListener('click', async (event) => {
      event.stopPropagation();
      if (!state.currentUser) {
        alert('Bitte logge dich ein, um Filme zu bewerten.');
        return;
      }

      const title = button.dataset.title;
      const score = Number(prompt(`Bewertung für ${title} eingeben (1-10):`, '8'));
      if (!Number.isFinite(score) || score < 1 || score > 10) {
        alert('Bitte nur Zahlen zwischen 1 und 10 eingeben.');
        return;
      }

      const response = await fetch('/api/rating', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ movieTitle: title, username: state.currentUser.username, score, reviewText: 'Bewertung via Browser' })
      });

      const result = await response.json();
      if (!response.ok) {
        alert(result.error || 'Bewertung fehlgeschlagen.');
        return;
      }

      alert('Danke für deine Bewertung!');
      await fetchMovies();
    });
  });
}

async function handleRegister(event) {
  event.preventDefault();
  const username = document.getElementById('registerUsername').value.trim();
  const password = document.getElementById('registerPassword').value;
  const favoriteGenres = document.getElementById('favoriteGenres').value
    .split(',')
    .map((item) => item.trim())
    .filter(Boolean);

  const response = await fetch('/api/register', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ username, password, favoriteGenres })
  });

  const result = await response.json();
  if (!response.ok) {
    alert(result.error || 'Registrierung fehlgeschlagen.');
    return;
  }

  state.currentUser = result.user;
  saveCurrentUser();
  updateUserUI();
  toggleModal(elements.registerModal, false);
  event.target.reset();
  alert('Registrierung erfolgreich!');
}

async function handleLogin(event) {
  event.preventDefault();
  const username = document.getElementById('loginUsername').value.trim();
  const password = document.getElementById('loginPassword').value;

  const response = await fetch('/api/login', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ username, password })
  });

  const result = await response.json();
  if (!response.ok) {
    alert(result.error || 'Login fehlgeschlagen.');
    return;
  }

  state.currentUser = result.user;
  saveCurrentUser();
  updateUserUI();
  toggleModal(elements.loginModal, false);
  event.target.reset();
  alert('Login erfolgreich!');
}

async function handleProfileUpdate(event) {
  event.preventDefault();
  if (!state.currentUser) {
    alert('Bitte zuerst anmelden.');
    return;
  }

  const username = elements.profileUsername.value.trim();
  const favoriteGenres = elements.profileGenres.value
    .split(',')
    .map((g) => g.trim())
    .filter(Boolean);

  const response = await fetch('/api/profile', {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ username: state.currentUser.username, newUsername: username, favoriteGenres })
  });

  const result = await response.json();
  if (!response.ok) {
    alert(result.error || 'Speichern fehlgeschlagen.');
    return;
  }

  state.currentUser = result.user;
  saveCurrentUser();
  updateUserUI();
  alert('Profil gespeichert.');
}

async function handleAddMovie(event) {
  event.preventDefault();
  const form = event.currentTarget;
  const payload = {
    title: form.title.value.trim(),
    year: Number(form.year.value),
    imageUrl: form.imageUrl.value.trim(),
    genres: form.genres.value.split(',').map((g) => g.trim()).filter(Boolean),
    cast: form.cast.value.split(',').map((c) => c.trim()).filter(Boolean),
    director: form.director.value.trim(),
    description: form.description.value.trim()
  };

  const response = await fetch('/api/movies', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(payload)
  });

  const result = await response.json();
  if (!response.ok) {
    alert(result.error || 'Film konnte nicht hinzugefügt werden.');
    return;
  }

  form.reset();
  window.location.href = `/detail.html?movie=${encodeURIComponent(result.movie.title)}`;
}

async function fetchMovieByTitle(title) {
  const response = await fetch(`/api/movies/by-title?title=${encodeURIComponent(title)}`);
  if (!response.ok) {
    throw new Error('Film konnte nicht geladen werden.');
  }
  return response.json();
}

function renderDetailPage(movie) {
  if (!movie) return;

  if (elements.detailTitle) elements.detailTitle.textContent = movie.title;
  if (elements.detailPoster) {
    const poster = movie.imageUrl || 'https://images.unsplash.com/photo-1517604931442-7e0c8ed2963c?auto=format&fit=crop&w=900&q=80';
    elements.detailPoster.style.background = `linear-gradient(135deg, rgba(0,0,0,0.15), rgba(10,10,10,0.6)), url('${poster}') center/cover no-repeat`;
  }
  if (elements.detailGenres) elements.detailGenres.innerHTML = (movie.genres || []).map((genre) => `<span class="genre-pill">${genre}</span>`).join('');
  if (elements.detailCast) elements.detailCast.textContent = (movie.cast || []).join(', ');
  if (elements.detailDirector) elements.detailDirector.textContent = movie.director || 'Unbekannt';
  if (elements.detailPlot) elements.detailPlot.textContent = movie.plot || 'Keine Beschreibung verfügbar.';
  if (elements.detailScore) elements.detailScore.textContent = `${Number(movie.averageRating || 0).toFixed(1)} / 10`;

  if (elements.reviewList) {
    const reviews = movie.reviews || [];
    if (!reviews.length) {
      elements.reviewList.innerHTML = '<div class="review-item"><strong>Keine Bewertungen</strong><p>Sei der Erste, der eine Rezension hinterlässt.</p></div>';
      return;
    }

    elements.reviewList.innerHTML = reviews.map((review) => `
      <div class="review-item">
        <strong>${review.username}</strong>
        <div>★ ${Number(review.score || 0).toFixed(1)}/10</div>
        <p>${review.comment || 'Keine Rezension hinterlassen.'}</p>
      </div>
    `).join('');
  }
}

function initSharedActions() {
  if (elements.loginBtn) {
    elements.loginBtn.addEventListener('click', () => toggleModal(elements.loginModal, true));
  }

  if (elements.registerBtn) {
    elements.registerBtn.addEventListener('click', () => toggleModal(elements.registerModal, true));
  }

  if (elements.logoutBtn) {
    elements.logoutBtn.addEventListener('click', () => {
      state.currentUser = null;
      saveCurrentUser();
      updateUserUI();
      if (window.location.pathname !== '/index.html') {
        window.location.href = '/index.html';
      }
    });
  }

  if (elements.settingsBtn) {
    elements.settingsBtn.addEventListener('click', () => {
      window.location.href = '/profile.html';
    });
  }

  document.querySelectorAll('.modal').forEach((modal) => {
    modal.addEventListener('click', (event) => {
      if (event.target === modal) {
        modal.classList.add('hidden');
      }
    });
  });

  if (elements.loginForm) {
    elements.loginForm.addEventListener('submit', handleLogin);
  }

  if (elements.registerForm) {
    elements.registerForm.addEventListener('submit', handleRegister);
  }

  updateUserUI();
}

function initDetailPage() {
  const params = new URLSearchParams(window.location.search);
  const movieName = params.get('movie');
  if (!movieName) return;

  fetchMovieByTitle(movieName)
    .then(renderDetailPage)
    .catch(() => {
      if (elements.detailTitle) {
        elements.detailTitle.textContent = 'Film nicht gefunden';
      }
    });

  const starButtons = document.querySelectorAll('.star-btn');
  starButtons.forEach((button) => {
    button.addEventListener('click', () => {
      state.reviewStars = Number(button.dataset.value);
      starButtons.forEach((btn) => btn.classList.toggle('active', Number(btn.dataset.value) <= state.reviewStars));
    });
  });

  if (elements.ratingForm) {
    elements.ratingForm.addEventListener('submit', async (event) => {
      event.preventDefault();
      if (!state.currentUser) {
        alert('Bitte logge dich ein, um zu bewerten.');
        return;
      }

      const reviewText = document.getElementById('reviewText').value.trim();
      const score = Math.max(1, Math.min(10, state.reviewStars * 2 || 8));

      const response = await fetch('/api/rating', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ movieTitle: movieName, username: state.currentUser.username, score, reviewText })
      });

      const result = await response.json();
      if (!response.ok) {
        alert(result.error || 'Bewertung fehlgeschlagen.');
        return;
      }

      event.target.reset();
      state.reviewStars = 0;
      document.querySelectorAll('.star-btn').forEach((btn) => btn.classList.remove('active'));
      await fetchMovieByTitle(movieName).then(renderDetailPage);
      alert('Bewertung gespeichert!');
    });
  }
}

function initProfilePage() {
  if (!elements.profileForm) return;

  if (state.currentUser) {
    elements.profileUsername.value = state.currentUser.username;
    elements.profileGenres.value = (state.currentUser.favoriteGenres || []).join(', ');
  }

  elements.profileForm.addEventListener('submit', handleProfileUpdate);
}

function initAddMoviePage() {
  if (!elements.addMovieForm) return;
  elements.addMovieForm.addEventListener('submit', handleAddMovie);
}

function initHomePage() {
  if (elements.searchBtn) {
    elements.searchBtn.addEventListener('click', () => {
      state.query = elements.globalSearch.value.trim();
      if (elements.searchSuggestions) {
        elements.searchSuggestions.classList.add('hidden');
      }
      fetchMovies();
    });
  }

  if (elements.globalSearch) {
    elements.globalSearch.addEventListener('input', () => {
      state.query = elements.globalSearch.value.trim();
      renderSearchSuggestions();
    });

    elements.globalSearch.addEventListener('keydown', (event) => {
      if (event.key === 'Enter') {
        state.query = elements.globalSearch.value.trim();
        if (elements.searchSuggestions) {
          elements.searchSuggestions.classList.add('hidden');
        }
        fetchMovies();
      }
    });

    elements.globalSearch.addEventListener('focus', () => {
      renderSearchSuggestions();
    });
  }

  document.addEventListener('click', (event) => {
    const isInsideSearch = event.target.closest('.search-wrap');
    if (!isInsideSearch && elements.searchSuggestions) {
      elements.searchSuggestions.classList.add('hidden');
    }
  });

  document.querySelectorAll('.chip').forEach((chip) => {
    chip.addEventListener('click', () => {
      document.querySelectorAll('.chip').forEach((item) => item.classList.remove('active'));
      chip.classList.add('active');
      state.genre = chip.dataset.genre || '';
      fetchMovies();
    });
  });

  if (elements.sortSelect) {
    elements.sortSelect.addEventListener('change', (event) => {
      state.sort = event.target.value;
      fetchMovies();
    });
  }

  fetchMovies();
}

if (page === 'home') {
  initSharedActions();
  initHomePage();
}
if (page === 'detail') {
  initSharedActions();
  initDetailPage();
}
if (page === 'profile') {
  initSharedActions();
  initProfilePage();
}
if (page === 'add') {
  initSharedActions();
  initAddMoviePage();
}
