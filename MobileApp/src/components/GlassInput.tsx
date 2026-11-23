import React from 'react';
import { View, Text, TextInput, StyleSheet, TextInputProps } from 'react-native';
import { useTheme } from '../theme/ThemeContext';

interface GlassInputProps extends TextInputProps {
  label?: string;
  error?: string;
  icon?: string;
}

export const GlassInput: React.FC<GlassInputProps> = ({ label, error, style, ...props }) => {
  const { theme } = useTheme();

  return (
    <View style={styles.container}>
      {label && <Text style={[styles.label, { color: theme.textSecondary }]}>{label}</Text>}
      <View
        style={[
          styles.inputWrapper,
          {
            backgroundColor: theme.inputBackground,
            borderColor: error ? theme.statusError : theme.inputBorder,
          },
        ]}
      >
        <TextInput
          placeholderTextColor={theme.inputPlaceholder}
          style={[styles.input, { color: theme.textPrimary }, style]}
          {...props}
        />
      </View>
      {error && <Text style={[styles.errorText, { color: theme.statusError }]}>{error}</Text>}
    </View>
  );
};

const styles = StyleSheet.create({
  container: {
    marginBottom: 16,
  },
  label: {
    fontSize: 13,
    fontWeight: '600',
    marginBottom: 6,
    letterSpacing: 0.5,
    textTransform: 'uppercase',
  },
  inputWrapper: {
    borderRadius: 12,
    borderWidth: 1,
    paddingHorizontal: 14,
    paddingVertical: 12,
    justifyContent: 'center',
  },
  input: {
    fontSize: 15,
    fontWeight: '500',
  },
  errorText: {
    fontSize: 12,
    marginTop: 4,
  },
});
