export type ThemeMode = 'glassmorphism' | 'darkGold' | 'light';

export interface ThemeColors {
  id: ThemeMode;
  name: string;
  background: string;
  backgroundGradient?: [string, string, ...string[]];
  cardBackground: string;
  cardBorder: string;
  cardShadow: string;
  textPrimary: string;
  textSecondary: string;
  textMuted: string;
  accent: string;
  accentGlow: string;
  accentText: string;
  inputBackground: string;
  inputBorder: string;
  inputPlaceholder: string;
  statusSuccess: string;
  statusError: string;
  statusWarning: string;
  isGlass: boolean;
  blurIntensity: number;
}
