/** @type {import('tailwindcss').Config} */
module.exports = {
  darkMode: 'media',
  content: [
    "./src/AnimalTracker/**/*.razor",
    "./src/AnimalTracker/**/*.cshtml",
    "./src/AnimalTracker/**/*.html",
    "./src/AnimalTracker/**/*.cs"
  ],
  theme: {
    extend: {}
  },
  plugins: []
};

