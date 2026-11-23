import React, { useState, useEffect } from 'react';
import { View, Text, StyleSheet, TouchableOpacity } from 'react-native';
import { useTheme } from '../theme/ThemeContext';
import { getServerUrl, testServerConnection } from '../config/serverConfig';

interface HeaderBarProps {
  title: string;
  onOpenConfig?: () => void;
}

export const HeaderBar: React.FC<HeaderBarProps> = ({ title, onOpenConfig }) => {
  const { theme, themeMode } = useTheme();
  const [isConnected, setIsConnected] = useState<boolean | null>(null);

  useEffect(() => {
    checkConnection();
  }, []);

  const checkConnection = async () => {
    const url = await getServerUrl();
    const result = await testServerConnection(url);
    setIsConnected(result.success);
  };

  return (
    <View style={[styles.header, { backgroundColor: theme.cardBackground, borderColor: theme.cardBorder }]}>
      <View style={styles.titleContainer}>
        <Text style={[styles.title, { color: theme.textPrimary }]}>{title}</Text>
        <Text style={[styles.subtitle, { color: theme.accent }]}>CONCEPT JEWELLERY PRO</Text>
      </View>

      <View style={styles.actions}>
        <View style={[styles.statusBadge, { backgroundColor: isConnected ? 'rgba(16, 185, 129, 0.15)' : 'rgba(239, 68, 68, 0.15)' }]}>
          <View style={[styles.dot, { backgroundColor: isConnected ? theme.statusSuccess : theme.statusError }]} />
          <Text style={[styles.statusText, { color: isConnected ? theme.statusSuccess : theme.statusError }]}>
            {isConnected ? 'ONLINE' : 'OFFLINE'}
          </Text>
        </View>

        {onOpenConfig && (
          <TouchableOpacity
            activeOpacity={0.7}
            onPress={onOpenConfig}
            style={[styles.configBtn, { backgroundColor: theme.inputBackground, borderColor: theme.inputBorder }]}
          >
            <Text style={{ fontSize: 16 }}>⚙️</Text>
          </TouchableOpacity>
        )}
      </View>
    </View>
  );
};

const styles = StyleSheet.create({
  header: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    paddingHorizontal: 20,
    paddingTop: 45,
    paddingBottom: 14,
    borderBottomWidth: 1,
  },
  titleContainer: {
    flex: 1,
  },
  title: {
    fontSize: 20,
    fontWeight: '800',
    letterSpacing: 0.5,
  },
  subtitle: {
    fontSize: 10,
    fontWeight: '700',
    letterSpacing: 1.5,
    marginTop: 2,
  },
  actions: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 10,
  },
  statusBadge: {
    flexDirection: 'row',
    alignItems: 'center',
    paddingHorizontal: 10,
    paddingVertical: 5,
    borderRadius: 20,
    gap: 6,
  },
  dot: {
    width: 6,
    height: 6,
    borderRadius: 3,
  },
  statusText: {
    fontSize: 10,
    fontWeight: '700',
    letterSpacing: 0.8,
  },
  configBtn: {
    width: 36,
    height: 36,
    borderRadius: 18,
    borderWidth: 1,
    alignItems: 'center',
    justifyContent: 'center',
  },
});
