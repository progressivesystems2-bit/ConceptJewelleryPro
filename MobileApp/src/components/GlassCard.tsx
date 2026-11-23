import React from 'react';
import { View, StyleSheet, ViewStyle, StyleProp, TouchableOpacity } from 'react-native';
import { useTheme } from '../theme/ThemeContext';

interface GlassCardProps {
  children: React.ReactNode;
  style?: StyleProp<ViewStyle>;
  onPress?: () => void;
  glow?: boolean;
}

export const GlassCard: React.FC<GlassCardProps> = ({ children, style, onPress, glow = false }) => {
  const { theme } = useTheme();

  const cardStyle: ViewStyle = {
    backgroundColor: theme.cardBackground,
    borderColor: glow ? theme.accent : theme.cardBorder,
    borderWidth: theme.isGlass ? 1 : 1,
    borderRadius: 18,
    padding: 16,
    shadowColor: glow ? theme.accent : theme.cardShadow,
    shadowOffset: { width: 0, height: 6 },
    shadowOpacity: theme.isGlass ? 0.35 : 0.15,
    shadowRadius: glow ? 12 : 8,
    elevation: theme.isGlass ? 8 : 3,
  };

  if (onPress) {
    return (
      <TouchableOpacity activeOpacity={0.8} onPress={onPress} style={[cardStyle, style]}>
        {children}
      </TouchableOpacity>
    );
  }

  return <View style={[cardStyle, style]}>{children}</View>;
};
