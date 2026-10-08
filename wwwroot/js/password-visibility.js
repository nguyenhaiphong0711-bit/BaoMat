document.addEventListener("click", (event) => {
    const toggle = event.target.closest("[data-password-toggle]");
    if (!toggle) return;

    const field = toggle.closest(".password-field");
    const input = field?.querySelector("[data-password-input]");
    if (!input) return;

    const reveal = input.type === "password";
    input.type = reveal ? "text" : "password";
    toggle.textContent = reveal ? "Ẩn" : "Hiện";
    toggle.setAttribute("aria-label", reveal ? "Ẩn mật khẩu" : "Hiển thị mật khẩu");
    toggle.setAttribute("aria-pressed", String(reveal));
});
