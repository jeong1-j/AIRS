const $ = (selector) => document.querySelector(selector);
const rules = window.thinkBackRules;
const language = Object.fromEntries(Object.entries(rules.language).map(([name, pattern]) =>
  [name, new RegExp(pattern, name === "Quotes" ? "gu" : "u")]));
const topics = rules.topics.map(([key, pattern, step, suggestion]) => ({
  pattern: new RegExp(pattern, "u"), step, suggestion,
}));
const compact = (text) => text.normalize("NFKC").replace(/[\s\p{P}\p{Cf}]+/gu, "").toLowerCase();
const storageKey = "think-back-hamster-v1";
const draft = $("#prompt");
const coach = $("#coach");
const pets = [$("#pet"), $("#walking-pet")];
const shopIcons = { seed: "🌻", berry: "🍓", apple: "🍎", carrot: "🥕", scarf: "🧣", crown: "👑", ribbon: "🎀", cap: "🌰" };

let progress = { points: 0, bond: 0, owned: [], earned: [], outfit: "", roam: false };
try {
  const saved = JSON.parse(localStorage.getItem(storageKey));
  if (saved && Array.isArray(saved.owned) && Array.isArray(saved.earned)) progress = { ...progress, ...saved };
} catch { /* The session remains usable when storage is unavailable. */ }

function splitClauses(text) {
  const clean = text.normalize("NFKC");
  const unquoted = clean.replace(language.Quotes, " ");
  return (language.QuoteCommand.test(unquoted) ? clean : unquoted)
    .split(language.Clauses).map((part) => part.trim()).filter(Boolean);
}

function findSignal(text) {
  const clauses = splitClauses(text);
  for (const [i, part] of clauses.entries()) {
    const clause = compact(part);
    const nearby = compact(clauses.slice(Math.max(0, i - 1), i + 2).join(" "));
    if (language.Mention.test(clause) || language.ClauseNegation.test(clause)) continue;
    let rule = -1;
    if (language.Skip.test(clause)) rule = 0;
    else if (language.Answer.test(clause) && !language.AnswerContext.test(nearby)) rule = 1;
    else if (language.HandOff.test(clause) &&
        (!language.Partial.test(clause) || language.ExplicitWhole.test(clause))) rule = 2;
    if (rule < 0) continue;
    const [kind, label, reason, baseStep, baseSuggestion] = rules.feedback[rule];
    const topic = rule !== 0 && (topics.find((candidate) => candidate.pattern.test(clause))
      || topics.find((candidate) => candidate.pattern.test(nearby)));
    const step = rule !== 0 && language.PriorWork.test(nearby)
      ? "이미 해본 부분은 살리고, 막힌 지점 한 곳만 골라 도움을 요청해 볼래요?"
      : (topic ? topic.step : baseStep);
    return {kind, label, reason, step, suggestion: topic ? topic.suggestion : baseSuggestion,
      evidence: part.length <= 76 ? part : part.slice(0, 73) + "…"};
  }
  return null;
}

function isGood(text) {
  let hasWork = false;
  return splitClauses(text).some((part) => {
    const clause = compact(part);
    if (language.Mention.test(clause) || language.GoodNegation.test(clause)) return false;
    hasWork ||= language.PriorWork.test(clause);
    return language.Constructive.test(clause) || (hasWork && language.Limited.test(clause)
      && language.ContextBridge.test(clause) && language.Completion.test(part));
  });
}

function questionKey(text) {
  // Keep a short fingerprint in browser storage instead of the draft.
  let value = 0xcbf29ce484222325n;
  for (const letter of compact(text)) {
    value = BigInt.asUintN(64, (value ^ BigInt(letter.codePointAt(0))) * 0x100000001b3n);
  }
  return value.toString(16);
}

function persist() {
  try { localStorage.setItem(storageKey, JSON.stringify(progress)); } catch { /* Session-only. */ }
  render();
}

function setFeedback(message, state = "") {
  $("#feedback").textContent = message;
  $("#feedback").dataset.state = state;
}

function showCoach(signal) {
  $("#coach-category").textContent = "AI 과의존 신호 · " + signal.label;
  $("#evidence").textContent = "“" + signal.evidence + "”";
  $("#reason").textContent = signal.reason;
  $("#step").textContent = signal.step;
  $("#suggestion").textContent = signal.suggestion;
  coach.hidden = false;
  setFeedback("햄스터가 먼저 해볼 한 단계를 제안했어요.");
}

let moodTimer;
function react(message, pose) {
  $("#pet-feedback").textContent = message;
  clearTimeout(moodTimer);
  const pet = pets[0];
  const food = $("#food-reaction");
  food.textContent = shopIcons[pose] || "";
  food.hidden = !food.textContent;
  pet.classList.remove("celebrate");
  pet.getBoundingClientRect();
  pet.classList.add("happy", "celebrate");
  pet.classList.toggle("peeking", pose === "peek");
  pet.querySelector("img").src = pose === "peek" ? "assets/hamster-peek.png" : "assets/hamster.png";
  moodTimer = setTimeout(() => {
    pet.classList.remove("celebrate", "peeking");
    pet.querySelector("img").src = "assets/hamster.png";
    food.hidden = true;
    render();
  }, 1800);
}

function checkQuestion() {
  clearTimeout(draftTimer);
  const text = draft.value.trim();
  coach.hidden = true;
  if (!text) {
    setFeedback("확인할 질문을 적어 주세요.");
    draft.focus();
    return;
  }

  const signal = findSignal(text);
  const goodQuestion = !signal && isGood(text);
  document.dispatchEvent(new CustomEvent("thinkback-check", {
    detail: { key: questionKey(text), risk: Boolean(signal), good: goodQuestion },
  }));
  if (signal) {
    showCoach(signal);
    return;
  }

  if (!goodQuestion) {
    setFeedback("내가 해본 부분이나 직접 확인하고 싶은 점을 덧붙여 볼래요?");
    return;
  }

  const key = questionKey(text);
  if (progress.earned.includes(key)) {
    setFeedback("이미 해바라기씨를 받은 질문이에요. 다음 질문도 써 볼까요?");
    return;
  }
  progress.earned.push(key);
  progress.points += rules.reward;
  persist();
  setFeedback("좋은 질문이에요! 해바라기씨 " + rules.reward + "알을 받았어요.", "good");
  react("햄스터가 폴짝 뛰었어요!", "happy");
}

const shop = rules.shop.map((item) => {
  const row = document.createElement("div");
  row.className = "item";
  const icon = document.createElement("span");
  icon.className = "item-icon";
  icon.textContent = shopIcons[item.id];
  icon.setAttribute("aria-hidden", "true");
  const detail = document.createElement("div");
  const name = document.createElement("strong");
  name.textContent = item.name;
  const note = document.createElement("small");
  note.textContent = item.bond ? "친밀도 +" + item.bond : "구매 후 다시 착용 가능";
  detail.append(name, note);
  const button = document.createElement("button");
  button.type = "button";
  button.className = "secondary";
  button.addEventListener("click", () => useItem(item));
  row.append(icon, detail, button);
  $("#shop").append(row);
  return { item, button };
});

function useItem(item) {
  if (progress.owned.includes(item.id)) {
    progress.outfit = progress.outfit === item.id ? "" : item.id;
  } else {
    if (progress.points < item.cost) return;
    progress.points -= item.cost;
    progress.bond += item.bond;
    if (item.kind === "outfit") {
      progress.owned.push(item.id);
      progress.outfit = item.id;
    }
  }
  persist();
  if (item.bond) {
    react("냠냠! " + item.name + " · 친밀도 +" + item.bond, item.id);
    document.dispatchEvent(new CustomEvent("airi-feed", { detail: shopIcons[item.id] }));
  }
  else react(progress.outfit ? item.name + " 착용!" : "옷을 벗었어요.", "happy");
}

function render() {
  $("#points").textContent = progress.points.toLocaleString("ko-KR") + "알";
  $("#bond").textContent = "♡ 친밀도 " + progress.bond;
  $("#mood").textContent = progress.bond >= 6 ? "단짝" : progress.bond >= 3 ? "친해지는 중" : "첫 만남";
  for (const pet of pets) {
    pet.dataset.outfit = progress.outfit;
    pet.classList.toggle("happy", progress.bond >= 3 || pet.classList.contains("celebrate"));
  }
  $("#walker").hidden = !progress.roam;
  $("#roam").textContent = progress.roam ? "산책 햄스터 숨기기" : "산책시키기";
  $("#roam").setAttribute("aria-pressed", progress.roam);
  for (const { item, button } of shop) {
    const owned = progress.owned.includes(item.id);
    button.textContent = owned ? (progress.outfit === item.id ? "벗기" : "입히기") : "씨앗 " + item.cost + "알";
    button.setAttribute("aria-label", item.name + " " + button.textContent);
    button.disabled = !owned && progress.points < item.cost;
  }
}

$("#prompt-form").addEventListener("submit", (event) => {
  event.preventDefault();
  checkQuestion();
});
let draftTimer;
draft.addEventListener("input", () => {
  clearTimeout(draftTimer);
  coach.hidden = true;
  setFeedback("작성 중");
  draftTimer = setTimeout(() => {
    const signal = findSignal(draft.value);
    if (signal) showCoach(signal);
  }, 650);
});
for (const button of document.querySelectorAll("[data-example]")) {
  button.addEventListener("click", () => {
    clearTimeout(draftTimer);
    draft.value = button.dataset.example;
    if (findSignal(draft.value)) checkQuestion();
    else {
      coach.hidden = true;
      setFeedback("예시를 내 풀이에 맞게 바꿔 보고, 질문 확인을 눌러 보세요.");
      draft.focus();
    }
  });
}
$("#apply").addEventListener("click", () => {
  draft.value = $("#suggestion").textContent;
  coach.hidden = true;
  setFeedback("내가 해본 부분을 덧붙인 뒤 질문을 확인해 보세요.");
  draft.focus();
});
$("#dismiss").addEventListener("click", () => {
  coach.hidden = true;
  draft.focus();
});
$("#keep").addEventListener("click", () => {
  coach.hidden = true;
  setFeedback("원래 질문을 유지했어요.");
  draft.focus();
});
$("#roam").addEventListener("click", () => {
  progress.roam = !progress.roam;
  persist();
  $("#pet-feedback").textContent = progress.roam ? "햄스터가 화면 아래를 산책해요." : "햄스터가 집에 돌아왔어요.";
});
$("#pet-play").addEventListener("click", () => react("빼꼼! 햄스터가 고개를 내밀었어요.", "peek"));
function showView() {
  const view = ["practice", "hamster", "weekly"].includes(location.hash.slice(1))
    ? location.hash.slice(1) : "practice";
  document.querySelectorAll("[data-panel]").forEach((panel) => { panel.hidden = panel.dataset.panel !== view; });
  document.querySelectorAll("[data-view]").forEach((link) => {
    if (link.dataset.view === view) link.setAttribute("aria-current", "page");
    else link.removeAttribute("aria-current");
  });
  window.scrollTo(0, 0);
}
addEventListener("hashchange", showView);
showView();
render();
