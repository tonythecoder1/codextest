document.addEventListener("DOMContentLoaded", () => {
  setupRevealMotion();
  setupNotificationMenu();
});

function setupRevealMotion() {
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
}

function setupNotificationMenu() {
  const tokenInput = document.querySelector(".notification-token-form input[name='__RequestVerificationToken']");
  const token = tokenInput?.value;

  if (!token) {
    return;
  }

  const postNotificationAction = async (url) => {
    const response = await fetch(url, {
      method: "POST",
      credentials: "same-origin",
      headers: {
        "RequestVerificationToken": token,
        "X-Requested-With": "XMLHttpRequest"
      }
    });

    if (!response.ok) {
      throw new Error("Notification action failed.");
    }
  };

  const refreshBadge = () => {
    const unreadItems = document.querySelectorAll(".notification-menu-item.is-unread").length;
    const badge = document.querySelector("[data-notification-badge]");
    const countLabel = document.querySelector(".notification-menu-header span");
    const markAllButton = document.querySelector("[data-notification-mark-all-url]");

    if (countLabel) {
      countLabel.textContent = `${unreadItems} por ler`;
    }

    if (badge) {
      if (unreadItems === 0) {
        badge.remove();
      } else {
        badge.textContent = Math.min(unreadItems, 9).toString();
      }
    }

    if (unreadItems === 0) {
      markAllButton?.remove();
    }
  };

  document.addEventListener("click", async (event) => {
    const notificationButton = event.target.closest("[data-notification-read-url]");
    if (notificationButton) {
      event.preventDefault();
      const targetUrl = notificationButton.dataset.notificationTargetUrl;

      try {
        notificationButton.disabled = true;
        await postNotificationAction(notificationButton.dataset.notificationReadUrl);
        notificationButton.closest(".notification-menu-item")?.classList.remove("is-unread");
        refreshBadge();

        if (targetUrl) {
          window.location.href = targetUrl;
        }
      } catch {
        notificationButton.disabled = false;
      }

      return;
    }

    const markAllButton = event.target.closest("[data-notification-mark-all-url]");
    if (!markAllButton) {
      return;
    }

    event.preventDefault();

    try {
      markAllButton.disabled = true;
      await postNotificationAction(markAllButton.dataset.notificationMarkAllUrl);
      document
        .querySelectorAll(".notification-menu-item.is-unread")
        .forEach((item) => item.classList.remove("is-unread"));
      refreshBadge();
    } catch {
      markAllButton.disabled = false;
    }
  });
}
