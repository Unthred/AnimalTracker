/** @type {import('tailwindcss').Config} */
module.exports = {
  darkMode: 'class',
  content: [
    "./src/AnimalTracker/**/*.razor",
    "./src/AnimalTracker/**/*.cshtml",
    "./src/AnimalTracker/**/*.html",
    "./src/AnimalTracker/**/*.cs"
  ],
  theme: {
    extend: {
      fontFamily: {
        sans: ['Inter', 'ui-sans-serif', 'system-ui', 'Segoe UI', 'sans-serif']
      },
      boxShadow: {
        card: '0 1px 2px 0 rgb(15 23 42 / 0.05), 0 2px 8px -2px rgb(15 23 42 / 0.07)'
      }
    }
  },
  plugins: []
};

