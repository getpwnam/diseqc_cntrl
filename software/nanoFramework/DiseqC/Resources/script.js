document.addEventListener("DOMContentLoaded", () => {
    const angleForm = document.getElementById("angleForm");
    const angleInput = document.getElementById("angle");
    const errorEl = document.getElementById("error");
    const okEl = document.getElementById("ok");

    // your original validation function, updated
    function validateAndSend() {
        const input = angleInput.value.trim();

        if (/^-?\d+$/.test(input)) {
            // Valid integer
            errorEl.style.display = "none";
            okEl.textContent = `Moving the rotor to ${parseInt(input, 10)}°!`;
            okEl.style.display = "block";
            return true;
        } else {
            // Invalid input
            errorEl.style.display = "block";
            okEl.style.display = "none";
            return false;
        }
    }

    if (angleForm) {
        angleForm.addEventListener("submit", async function (e) {
            e.preventDefault(); // stop browser redirect

            if (!validateAndSend()) {
                return; // don't call API if invalid
            }

            const angle = angleInput.value.trim();

            try {
                let response = await fetch("/angle", {
                    method: "POST",
                    headers: { "Content-Type": "application/x-www-form-urlencoded" },
                    body: "angle=" + encodeURIComponent(angle)
                });

                let text = await response.text();

                if (text.includes("Invalid")) {
                    errorEl.style.display = "block";
                    okEl.style.display = "none";
                }
            } catch (err) {
                console.error("Fetch failed:", err);
                errorEl.style.display = "block";
                okEl.style.display = "none";
            }
        });
    }

    // --- Read potentiometer once when clicking the button ---
    const potInput = document.getElementById("pot_angle");
    const readPotButton = document.getElementById("readPot");

    if (readPotButton) {
        readPotButton.addEventListener("click", async () => {
            try {
                let response = await fetch("/pot", { method: "GET" });
                if (!response.ok) return;

                let text = await response.text();

                if (!isNaN(text)) {
                    potInput.value = parseFloat(text);
                }
            } catch (err) {
                console.error("Failed to fetch potentiometer angle:", err);
            }
        });
    }
});