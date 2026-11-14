(() => {
  const walker = document.querySelector("#walker");
  const pet = walker.querySelector(".pet");
  const canvas = pet.querySelector("canvas");
  const context = canvas.getContext("2d");
  const { frames, rests, restFrameSeconds } = window.airiPoses;
  const atlas = new Image();
  const reducedMotion = matchMedia("(prefers-reduced-motion: reduce)");
  let x = 22, y = 10, targetX = x, targetY = y;
  let pause, restElapsed, behavior, speed = 0, travel = 0, facing = 1, pace = 48;
  let previous = 0, frame = 0, pose = 4, drawn = -1, arrivals = 0;

  const clamp = (value, min, max) => Math.max(min, Math.min(max, value));

  function beginRest(next) {
    behavior = next;
    restElapsed = 0;
    pose = 4;
    pause = rests[next].length * restFrameSeconds + .18 + Math.random() * .55;
  }

  function paint() {
    if (!atlas.naturalWidth || drawn === pose) return;
    const [sx, sy, sw, sh] = frames[pose];
    const scale = Math.min(canvas.width / 330, canvas.height / 370);
    const w = sw * scale, h = sh * scale;
    const left = (canvas.width - w) / 2, top = canvas.height * .91 - h;
    context.clearRect(0, 0, canvas.width, canvas.height);
    context.imageSmoothingQuality = "high";
    context.drawImage(atlas, sx, sy, sw, sh, left, top, w, h);
    const upright = pose >= 7 && pose <= 10;
    pet.dataset.pose = pose;
    pet.toggleAttribute("data-upright", upright);
    const percent = (n) => n / canvas.width * 100 + "%";
    pet.style.setProperty("--scarf-x", percent(left + w * (upright ? .36 : .55)));
    pet.style.setProperty("--scarf-y", percent(top + h * .55));
    pet.style.setProperty("--crown-x", percent(left + w * .56));
    pet.style.setProperty("--crown-y", percent(top - h * .08));
    drawn = pose;
  }

  function animate(now) {
    frame = 0;
    if (walker.hidden || document.hidden) return;
    const dt = previous ? Math.min((now - previous) / 1000, .08) : 0;
    previous = now;
    const right = Math.max(10, innerWidth - walker.offsetWidth - 10);
    const top = 10 + Math.max(0, Math.min(150, innerHeight * .24, innerHeight - walker.offsetHeight - 20));
    x = clamp(x, 10, right);
    y = clamp(y, 10, top);
    targetX = clamp(targetX, 10, right);
    targetY = clamp(targetY, 10, top);

    if (!reducedMotion.matches) {
      const dx = targetX - x, dy = targetY - y;
      const distance = Math.hypot(dx, dy);
      if (distance <= 1) {
        speed = 0;
        restElapsed += dt;
        const sequence = rests[behavior];
        pose = sequence[Math.min(sequence.length - 1, Math.floor(restElapsed / restFrameSeconds))];
        pause -= dt;
        if (pause <= 0) {
          const leg = (90 + 170 * Math.random()) * (Math.random() < .5 ? 1 : -1);
          const next = x + leg;
          targetX = clamp(next < 10 || next > right ? x - leg : next, 10, right);
          targetY = clamp(y + (Math.random() - .5) * 48, 10, top);
          if (Math.abs(targetX - x) > 5) facing = targetX < x ? -1 : 1;
          pace = 35 + Math.random() * 22;
          travel = 0;
        }
      } else {
        speed = Math.min(pace, Math.sqrt(2 * 100 * distance), speed + dt * 100);
        const step = Math.min(distance, speed * dt);
        x += (dx / distance) * step;
        y += (dy / distance) * step;
        travel += step;
        pose = Math.floor(travel / 5.5) % 4;
        if (distance - step <= 1) {
          x = targetX;
          y = targetY;
          arrivals++;
          beginRest(arrivals % 5 === 0 ? 4 : 1 + Math.floor(Math.random() * 3));
        }
      }
    } else pose = 4;

    walker.style.transform = "translate3d(" + x + "px," + -y + "px,0)";
    walker.style.setProperty("--facing", facing);
    paint();
    if (!reducedMotion.matches) frame = requestAnimationFrame(animate);
  }

  function refresh() {
    cancelAnimationFrame(frame);
    frame = 0;
    previous = 0;
    if (!walker.hidden && !document.hidden) frame = requestAnimationFrame(animate);
  }

  new MutationObserver(refresh).observe(walker, { attributes: true, attributeFilter: ["hidden"] });
  document.addEventListener("visibilitychange", refresh);
  reducedMotion.addEventListener("change", refresh);
  addEventListener("resize", () => { if (reducedMotion.matches) refresh(); });
  beginRest(1);
  atlas.addEventListener("load", paint);
  atlas.src = "assets/airi-poses.png";
  refresh();
})();
