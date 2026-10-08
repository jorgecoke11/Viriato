# Viariato — frontend

React 18 + TypeScript + Vite + Tailwind 4. Tests de la lógica pura con `npm test` (vitest); `npm run lint`; `npm run build`.

## Sistema de diseño

Todo lo visual se decide en `src/index.css`; los componentes no llevan colores propios.

**Colores.** La interfaz está escrita con los nombres de color de Tailwind (`gray`, `indigo`, `blue`, `red`, `green`,
`amber`, `purple`), pero esos nombres apuntan a variables CSS (`--c-gray-500`…), así que un solo sitio define qué es
«gray-500» o «green-100» y el **tema oscuro** es el mismo vocabulario con otros valores (bloque `.dark`), no un segundo
juego de clases. Qué significa cada uno:

| Color | Para qué |
|---|---|
| `gray` | Neutro frío. 50-100 rellenos, 200-300 bordes, 400 iconos y placeholders, 500-600 texto secundario, 800-900 texto principal. Se invierte en oscuro. |
| `indigo` | Marca y acción principal. |
| `blue` / `green` / `amber` / `red` / `purple` | Estado: en curso / hecho / en espera o pausa / fallo / necesita a una persona. **Nunca solo color**: las insignias llevan un punto o un icono, y siempre texto. |
| `page`, `surface`, `raised` | El lienzo, las tarjetas sobre él y lo que flota (diálogos, menús). |
| `side-*` | La barra de navegación, que es oscura en los dos temas. |

Tipografía: **Fira Sans** para la interfaz y **Fira Code** para datos (ids, contadores, JSON), autoalojadas con `@fontsource`.

**Tema.** Claro, oscuro o automático (sigue al sistema), con selector en la barra lateral; se recuerda en el navegador.
`index.html` aplica la clase `dark` antes del primer pintado para que no haya destello. `src/lib/theme/`.

**Componentes genéricos** (`src/components/ui/`): `Button` (primary / secondary / ghost / danger, tamaños sm y md),
`IconButton` (siempre con nombre accesible), `Card`, `Input` y la clase `.field` para selects y textareas, `Modal`
(Escape cierra, `role="dialog"`), `Tabs`, `Badge`, `ActivoBadge`, `Switch`, `PageHeader`, `StatCard`, `EmptyState`,
`Skeleton`, `BackLink`, `ConfirmDialog`. El CRUD genérico (`src/components/crud/`) y el formulario generado desde un
esquema JSON (`src/components/schema-form/`) se construyen con ellos.

Reglas que se mantienen: una sola clase de campo (`.field`), un solo estilo de título (`.page-title`), el foco siempre
visible, objetivos táctiles de 40px en móvil, `prefers-reduced-motion` respetado y contraste de texto de 4.5:1.

---

## Plantilla de Vite

This template provides a minimal setup to get React working in Vite with HMR and some ESLint rules.

Currently, two official plugins are available:

- [@vitejs/plugin-react](https://github.com/vitejs/vite-plugin-react/blob/main/packages/plugin-react/README.md) uses [Babel](https://babeljs.io/) for Fast Refresh
- [@vitejs/plugin-react-swc](https://github.com/vitejs/vite-plugin-react-swc) uses [SWC](https://swc.rs/) for Fast Refresh

## Expanding the ESLint configuration

If you are developing a production application, we recommend updating the configuration to enable type aware lint rules:

- Configure the top-level `parserOptions` property like this:

```js
export default tseslint.config({
  languageOptions: {
    // other options...
    parserOptions: {
      project: ['./tsconfig.node.json', './tsconfig.app.json'],
      tsconfigRootDir: import.meta.dirname,
    },
  },
})
```

- Replace `tseslint.configs.recommended` to `tseslint.configs.recommendedTypeChecked` or `tseslint.configs.strictTypeChecked`
- Optionally add `...tseslint.configs.stylisticTypeChecked`
- Install [eslint-plugin-react](https://github.com/jsx-eslint/eslint-plugin-react) and update the config:

```js
// eslint.config.js
import react from 'eslint-plugin-react'

export default tseslint.config({
  // Set the react version
  settings: { react: { version: '18.3' } },
  plugins: {
    // Add the react plugin
    react,
  },
  rules: {
    // other rules...
    // Enable its recommended rules
    ...react.configs.recommended.rules,
    ...react.configs['jsx-runtime'].rules,
  },
})
```
