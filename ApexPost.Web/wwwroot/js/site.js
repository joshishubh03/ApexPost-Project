// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.
(() => {
    const shipmentType = document.getElementById("ShipmentType");
    const dimensions = document.querySelector("[data-parcel-dimensions]");
    if (!shipmentType || !dimensions) return;

    const updateDimensions = () => {
        dimensions.hidden = shipmentType.value === "Letter" || shipmentType.value === "";
        if (dimensions.hidden) {
            dimensions.querySelectorAll("input").forEach(input => { input.value = ""; });
        }
    };
    shipmentType.addEventListener("change", updateDimensions);
    updateDimensions();
})();
