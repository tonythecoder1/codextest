document.addEventListener("DOMContentLoaded", () => {
  const revealTargets = document.querySelectorAll(
    ".hero-panel, .card-surface, .stat-card, .entry-item, .summary-item, .app-alert"
  );

  if (revealTargets.length === 0) {
    return;
  }

  document.body.classList.add("motion-ready");

  revealTargets.forEach((element, index) => {
    element.classList.add("revealable");
    element.style.transitionDelay = `${Math.min(index * 45, 240)}ms`;
  });

  const observer = new IntersectionObserver(
    (entries) => {
      entries.forEach((entry) => {
        if (!entry.isIntersecting) {
          return;
        }

        entry.target.classList.add("is-visible");
        observer.unobserve(entry.target);
      });
    },
    {
      threshold: 0.14,
      rootMargin: "0px 0px -8% 0px"
    }
  );

  revealTargets.forEach((element) => observer.observe(element));
});
