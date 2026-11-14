(() => {
  const get = (id) => document.getElementById(id);
  const storageKey = "think-back-weekly-v1";
  let days = {};
  try { days = JSON.parse(localStorage.getItem(storageKey)) || {}; } catch { /* Session-only. */ }

  const offset = (date, amount) => new Date(date.getFullYear(), date.getMonth(), date.getDate() + amount);
  const monday = (date) => offset(date, -((date.getDay() + 6) % 7));
  const dateKey = (date) =>
    [date.getFullYear(), date.getMonth() + 1, date.getDate()].map((part) => String(part).padStart(2, "0")).join("-");
  const shortDate = (date) => (date.getMonth() + 1) + "." + date.getDate();
  const number = new Intl.NumberFormat("ko-KR", { maximumFractionDigits: 1 });
  let week = monday(new Date());

  function readWeek(start) {
    return Array.from({ length: 7 }, (_, index) => {
      const date = offset(start, index);
      return { date, total: 0, risk: 0, good: 0, ...days[dateKey(date)] };
    });
  }

  function summarize(entries) {
    return entries.reduce((sum, entry) => ({
      total: sum.total + entry.total,
      risk: sum.risk + entry.risk,
      good: sum.good + entry.good,
    }), { total: 0, risk: 0, good: 0 });
  }

  function drawMatrix() {
    const periods = Array.from({ length: 4 }, (_, i) => readWeek(offset(week, (i - 3) * 7)));
    const peak = Math.max(1, ...periods.flat().map(({ total }) => total));
    const today = dateKey(new Date());
    const rows = periods.map((entries, index) => {
      const row = document.createElement("tr");
      row.classList.toggle("selected-week", index === periods.length - 1);
      const heading = document.createElement("th");
      heading.scope = "row";
      heading.textContent = shortDate(entries[0].date);
      row.append(heading);
      for (const entry of entries) {
        const cell = document.createElement("td");
        const future = dateKey(entry.date) > today;
        cell.dataset.level = future ? "future" : entry.total ? String(Math.ceil(4 * entry.total / peak)) : "0";
        cell.textContent = entry.total || "·";
        cell.title = shortDate(entry.date) + (future ? " · 예정" : " · 질문 " + entry.total + "건");
        cell.setAttribute("aria-label", cell.title);
        row.append(cell);
      }
      return row;
    });
    get("activity-matrix").replaceChildren(...rows);
  }

  function render() {
    const entries = readWeek(week);
    const current = summarize(entries);
    const previous = summarize(readWeek(offset(week, -7)));
    const rate = current.total ? (100 * current.risk) / current.total : null;
    const delta = rate === null || !previous.total ? null : Math.round(10 * (rate - (100 * previous.risk) / previous.total)) / 10;

    get("week-range").textContent = week.getFullYear() + ". " + shortDate(week) + " – " + shortDate(offset(week, 6));
    get("next-week").disabled = week >= monday(new Date());
    get("risk-rate").textContent = rate === null ? "—" : number.format(rate) + "%";
    get("week-total").textContent = current.total + "건";
    get("week-good").textContent = current.good + "건";
    const other = Math.max(0, current.total - current.risk - current.good);
    get("week-risk").textContent = current.risk + "건";
    get("mix-good").textContent = current.good + "건";
    get("mix-other").textContent = other + "건";
    get("active-days").textContent = "활동 " + entries.filter(({ total }) => total > 0).length + "일";
    get("donut-total").textContent = current.total;
    const goodStop = current.total ? 100 * current.good / current.total : 0;
    const riskStop = current.total ? 100 * (current.good + current.risk) / current.total : 0;
    const ring = get("week-donut");
    ring.style.setProperty("--good-stop", goodStop + "%");
    ring.style.setProperty("--risk-stop", riskStop + "%");
    ring.classList.toggle("empty", !current.total);
    const change = get("week-change");
    change.textContent = delta === null ? (current.total ? "비교할 전주 기록이 없어요" : "아직 기록이 없어요")
      : delta === 0 ? "전주와 같아요" : "전주보다 " + number.format(Math.abs(delta)) + "%p " + (delta > 0 ? "높아요" : "낮아요");
    change.dataset.trend = delta > 0 ? "up" : delta < 0 ? "down" : "";

    const peak = Math.max(1, ...entries.map((entry) => entry.total));
    const columns = entries.map((entry, index) => {
      const cell = document.createElement("li");
      cell.setAttribute("aria-label", shortDate(entry.date) + " 확인 " + entry.total + "건, 과의존 신호 " + entry.risk + "건, 스스로 확인 " + entry.good + "건");
      const value = document.createElement("span");
      value.className = "day-value";
      value.textContent = entry.total || "–";
      const track = document.createElement("div");
      track.className = "day-track";
      const bar = document.createElement("span");
      bar.className = "day-bar";
      bar.style.height = (entry.total ? (entry.total / peak) * 100 : 0) + "%";
      for (const [kind, count] of [["good", entry.good], ["risk", entry.risk], ["other", Math.max(0, entry.total - entry.good - entry.risk)]]) {
        if (!count) continue;
        const segment = document.createElement("i");
        segment.className = "segment-" + kind;
        segment.style.flexGrow = count;
        bar.append(segment);
      }
      track.append(bar);
      const label = document.createElement("span");
      label.className = "day-label";
      label.textContent = "월화수목금토일"[index];
      cell.append(value, track, label);
      return cell;
    });
    get("week-chart").replaceChildren(...columns);
    drawMatrix();
  }

  document.addEventListener("thinkback-check", ({ detail }) => {
    const date = dateKey(new Date());
    const entry = days[date] ??= { total: 0, risk: 0, good: 0, keys: [] };
    if (entry.keys.includes(detail.key)) return;
    entry.keys.push(detail.key);
    entry.total++;
    if (detail.risk) entry.risk++;
    if (detail.good) entry.good++;
    try { localStorage.setItem(storageKey, JSON.stringify(days)); } catch { /* Session-only. */ }
    render();
  });
  get("previous-week").addEventListener("click", () => { week = offset(week, -7); render(); });
  get("next-week").addEventListener("click", () => { week = offset(week, 7); render(); });
  render();
})();
