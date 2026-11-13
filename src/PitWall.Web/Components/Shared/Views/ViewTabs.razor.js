// Cambio de vista dentro de la raíz interactiva: la navegación mejorada de Blazor rearma la raíz (se pierde el
// estado de los controles), así que el click en una pestaña se resuelve acá: se actualiza
// la URL con pushState y .NET cambia la vista. Ctrl/Shift/rueda siguen abriendo la vista en otra ventana.
export function register(nav, dotNet) {
    const onClick = event => {
        const tab = event.target instanceof Element ? event.target.closest("a[data-view-key]") : null;
        if (!tab || event.button !== 0 || event.ctrlKey || event.metaKey || event.shiftKey || event.altKey) {
            return;
        }

        // stopPropagation: el listener de navegación de Blazor está en document y no debe ver este click.
        event.preventDefault();
        event.stopPropagation();
        if (tab.getAttribute("aria-current") === "page") {
            return;
        }

        history.pushState(null, "", tab.href);
        document.title = `PitWall · ${tab.dataset.viewTitle}`;
        dotNet.invokeMethodAsync("Select", tab.dataset.viewRoute);
    };

    // Atajos 1–4: hacen click en la pestaña, así el cambio es el mismo que con el mouse. No actúan si se está
    // escribiendo en un campo ni con modificadores (Ctrl+1 es del navegador).
    const onKeyDown = event => {
        // Si la raíz se rearmó (Atrás/Adelante usan la navegación de Blazor) y el dispose no llegó a correr, este
        // listener queda huérfano: no debe tomar la tecla antes que el de las pestañas vigentes.
        if (!nav.isConnected) {
            document.removeEventListener("keydown", onKeyDown);
            return;
        }

        if (event.defaultPrevented || event.repeat || event.ctrlKey || event.metaKey || event.altKey) {
            return;
        }

        const target = event.target;
        if (target instanceof Element && (target.isContentEditable || target.closest("input, textarea, select"))) {
            return;
        }

        const tab = nav.querySelector(`a[data-view-key="${CSS.escape(event.key)}"]`);
        if (!tab) {
            return;
        }

        event.preventDefault();
        tab.click();
    };

    nav.addEventListener("click", onClick);
    document.addEventListener("keydown", onKeyDown);
    return {
        dispose: () => {
            nav.removeEventListener("click", onClick);
            document.removeEventListener("keydown", onKeyDown);
        },
    };
}
