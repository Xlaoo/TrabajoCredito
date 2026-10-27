window.addEventListener("beforeunload", function () {
    navigator.sendBeacon("/Login/MarcarClienteInactivo");
});
