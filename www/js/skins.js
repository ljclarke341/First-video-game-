// Themes are the cosmetic *and* the palette: they recolor the player, the gates
// and the background together. Shape glyphs (circle / triangle / square) carry
// the actual gameplay signal, so every theme stays readable for colorblind
// players and none of them can be "pay to see better".
export const SKINS = [
  {
    id: 'neon', name: 'NEON', cost: 0,
    colors: ['#36f5d0', '#ff4d9d', '#ffd23f'],
    bg: '#06060d', grid: 'rgba(120,140,255,.10)'
  },
  {
    id: 'sunset', name: 'SUNSET', cost: 150,
    colors: ['#ff9f45', '#ff5d8f', '#9b5de5'],
    bg: '#120714', grid: 'rgba(255,150,120,.10)'
  },
  {
    id: 'grove', name: 'GROVE', cost: 500,
    colors: ['#9bf6a0', '#2ec4b6', '#f6f7a0'],
    bg: '#04120e', grid: 'rgba(110,255,190,.09)'
  },
  {
    id: 'candy', name: 'CANDY', cost: 900,
    colors: ['#ff8fcf', '#7ad7ff', '#fff3b0'],
    bg: '#14081a', grid: 'rgba(255,180,230,.11)'
  },
  {
    id: 'circuit', name: 'CIRCUIT', cost: 1500,
    colors: ['#f2f5ff', '#ff3b3b', '#3b8cff'],
    bg: '#0a0a0a', grid: 'rgba(200,210,255,.10)'
  },
  {
    id: 'void', name: 'VOID', cost: 2500,
    colors: ['#b388ff', '#00e5ff', '#ff6ec7'],
    bg: '#04030c', grid: 'rgba(160,110,255,.13)'
  }
];

export function getSkin(id) {
  return SKINS.find(s => s.id === id) || SKINS[0];
}
