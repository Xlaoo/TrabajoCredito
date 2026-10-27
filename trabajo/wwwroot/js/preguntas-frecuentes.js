document.addEventListener("DOMContentLoaded", function () {
    document.querySelectorAll(".faq-question").forEach(btn => {
        btn.addEventListener("click", function () {
            const item = this.parentElement;
            const icon = this.querySelector("b");

            document.querySelectorAll(".faq-item").forEach(i => {
                if (i !== item) {
                    i.classList.remove("active");
                    const otherIcon = i.querySelector("b");
                    if (otherIcon) {
                        otherIcon.innerText = "+";
                    }
                }
            });

            item.classList.toggle("active");
            if (icon) {
                icon.innerText = item.classList.contains("active") ? "−" : "+";
            }
        });
    });
});
