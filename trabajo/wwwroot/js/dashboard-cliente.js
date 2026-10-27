let clientes = [];
let indiceActual = 0;
let calificacionSeleccionada = 0;

document.addEventListener("DOMContentLoaded", function () {
    const dataEl = document.getElementById("clientesData");
    if (dataEl && dataEl.textContent) {
        try {
            clientes = JSON.parse(dataEl.textContent);
        } catch (e) {
            clientes = [];
        }
    }

    renderClientes();

    const btnLeft = document.getElementById("btnLeft");
    if (btnLeft) {
        btnLeft.addEventListener("click", function () {
            moverClientes(-1);
        });
    }

    const btnRight = document.getElementById("btnRight");
    if (btnRight) {
        btnRight.addEventListener("click", function () {
            moverClientes(1);
        });
    }

    const estrellas = document.querySelectorAll(".estrellas-resena .estrella");
    estrellas.forEach(function (estrella) {
        estrella.addEventListener("click", function () {
            const valor = Number(this.dataset.estrella);
            seleccionarEstrella(valor);
        });
    });

    const btnEnviar = document.getElementById("btnEnviarResena");
    if (btnEnviar) {
        btnEnviar.addEventListener("click", enviarResena);
    }
});

function renderClientes() {
    const grid = document.getElementById("clientsGrid");
    if (!grid) return;

    grid.innerHTML = "";
    const visibles = clientes.slice(indiceActual, indiceActual + 3);

    visibles.forEach(function (c) {
        let estrellas = "";
        for (let i = 0; i < c.estrellas; i++) {
            estrellas += "⭐";
        }

        const card = document.createElement("div");
        card.className = "client-card";

        const icon = document.createElement("i");
        icon.className = "fas fa-user-circle";

        const info = document.createElement("div");

        const strong = document.createElement("strong");
        strong.textContent = c.nombre;

        const p = document.createElement("p");
        p.textContent = c.comentario;

        const starsSpan = document.createElement("span");
        starsSpan.textContent = estrellas;

        info.appendChild(strong);
        info.appendChild(p);
        info.appendChild(starsSpan);

        card.appendChild(icon);
        card.appendChild(info);

        grid.appendChild(card);
    });

    const btnLeft = document.getElementById("btnLeft");
    const btnRight = document.getElementById("btnRight");

    if (btnLeft) btnLeft.disabled = (indiceActual === 0);
    if (btnRight) btnRight.disabled = (indiceActual + 3 >= clientes.length);
}

function moverClientes(direccion) {
    if (direccion === 1 && indiceActual + 3 < clientes.length) {
        indiceActual++;
    }
    if (direccion === -1 && indiceActual > 0) {
        indiceActual--;
    }
    renderClientes();
}

function seleccionarEstrella(valor) {
    calificacionSeleccionada = valor;
    document.querySelectorAll(".estrellas-resena .estrella").forEach(function (estrella, index) {
        estrella.classList.toggle("activa", index < valor);
    });

    const err = document.getElementById("errorResena");
    if (err) err.style.display = "none";
}

function enviarResena() {
    const comentarioEl = document.getElementById("comentarioResena");
    const comentario = comentarioEl ? comentarioEl.value.trim() : "";
    const errorEl = document.getElementById("errorResena");

    if (calificacionSeleccionada === 0 || comentario === "") {
        if (errorEl) errorEl.style.display = "block";
        return;
    }

    const csrfToken = document.querySelector('input[name="__RequestVerificationToken"]')?.value || "";

    const params = new URLSearchParams();
    params.append("calificacion", calificacionSeleccionada);
    params.append("comentario", comentario);
    if (csrfToken) {
        params.append("__RequestVerificationToken", csrfToken);
    }

    const headers = {
        "Content-Type": "application/x-www-form-urlencoded"
    };
    if (csrfToken) {
        headers["RequestVerificationToken"] = csrfToken;
    }

    fetch("/Login/GuardarResena", {
        method: "POST",
        headers: headers,
        body: params.toString()
    })
    .then(function (r) {
        return r.json();
    })
    .then(function (data) {
        alert(data.mensaje);
        if (data.ok) {
            const modal = document.getElementById("modalResena");
            if (modal) modal.style.display = "none";
        }
    })
    .catch(function () {
        alert("Error al guardar la reseña.");
    });
}
