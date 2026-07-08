import '@testing-library/jest-dom'

// jsdom no implementa ResizeObserver ni Element.scrollIntoView; cmdk (usado por
// components/ui/command.tsx, el combobox de empresa) los necesita al montar/navegar.
if (typeof globalThis.ResizeObserver === 'undefined') {
  globalThis.ResizeObserver = class ResizeObserver {
    observe() {}
    unobserve() {}
    disconnect() {}
  }
}

if (typeof Element.prototype.scrollIntoView !== 'function') {
  Element.prototype.scrollIntoView = () => {}
}
