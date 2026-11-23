import React, { createContext, useContext, useState, useEffect } from 'react';
import AsyncStorage from '@react-native-async-storage/async-storage';
import { ThemeColors, ThemeMode } from './types';
import { GlassmorphismTheme } from './GlassmorphismTheme';
import { DarkGoldTheme } from './DarkGoldTheme';
import { LightTheme } from './LightTheme';

const THEME_STORAGE_KEY = '@concept_jewellery_theme';

interface ThemeContextType {
  theme: ThemeColors;
  themeMode: ThemeMode;
  setThemeMode: (mode: ThemeMode) => Promise<void>;
}

const ThemeContext = createContext<ThemeContextType>({
  theme: GlassmorphismTheme,
  themeMode: 'glassmorphism',
  setThemeMode: async () => {},
});

export const ThemeProvider: React.FC<{ children: React.ReactNode }> = ({ children }) => {
  const [themeMode, setThemeModeState] = useState<ThemeMode>('glassmorphism');

  useEffect(() => {
    loadSavedTheme();
  }, []);

  const loadSavedTheme = async () => {
    try {
      const saved = await AsyncStorage.getItem(THEME_STORAGE_KEY);
      if (saved && (saved === 'glassmorphism' || saved === 'darkGold' || saved === 'light')) {
        setThemeModeState(saved as ThemeMode);
      }
    } catch (e) {
      console.warn('Failed to load theme preference', e);
    }
  };

  const setThemeMode = async (mode: ThemeMode) => {
    try {
      setThemeModeState(mode);
      await AsyncStorage.setItem(THEME_STORAGE_KEY, mode);
    } catch (e) {
      console.warn('Failed to save theme preference', e);
    }
  };

  const getThemeColors = (mode: ThemeMode): ThemeColors => {
    switch (mode) {
      case 'glassmorphism':
        return GlassmorphismTheme;
      case 'darkGold':
        return DarkGoldTheme;
      case 'light':
        return LightTheme;
      default:
        return GlassmorphismTheme;
    }
  };

  return (
    <ThemeContext.Provider
      value={{
        theme: getThemeColors(themeMode),
        themeMode,
        setThemeMode,
      }}
    >
      {children}
    </ThemeContext.Provider>
  );
};

export const useTheme = () => useContext(ThemeContext);
