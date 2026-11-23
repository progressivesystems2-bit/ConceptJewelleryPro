import React, { useState, useEffect } from 'react';
import { View, Text, StyleSheet, ScrollView, TouchableOpacity, Alert } from 'react-native';
import { useTheme } from '../theme/ThemeContext';
import { ThemeMode } from '../theme/types';
import { getServerUrl, saveServerUrl, testServerConnection, ConnectionTestResult } from '../config/serverConfig';
import { GlassCard } from '../components/GlassCard';
import { GlassInput } from '../components/GlassInput';
import { GlassButton } from '../components/GlassButton';
import { useAuth } from '../context/AuthContext';

export const ConfigScreen: React.FC = () => {
  const { theme, themeMode, setThemeMode } = useTheme();
  const { user, logout } = useAuth();

  const [serverUrl, setServerUrl] = useState<string>('');
  const [testing, setTesting] = useState<boolean>(false);
  const [testResult, setTestResult] = useState<ConnectionTestResult | null>(null);

  useEffect(() => {
    loadCurrentUrl();
  }, []);

  const loadCurrentUrl = async () => {
    const current = await getServerUrl();
    setServerUrl(current);
  };

  const handleTest = async () => {
    if (!serverUrl.trim()) return;
    setTesting(true);
    setTestResult(null);
    const res = await testServerConnection(serverUrl);
    setTestResult(res);
    setTesting(false);
  };

  const handleSave = async () => {
    if (!serverUrl.trim()) {
      Alert.alert('Error', 'Please enter a valid server URL');
      return;
    }
    await saveServerUrl(serverUrl);
    Alert.alert('Success', 'Server URL updated successfully!');
  };

  const themeOptions: { id: ThemeMode; title: string; subtitle: string; icon: string }[] = [
    {
      id: 'glassmorphism',
      title: 'Glassmorphism ✨',
      subtitle: 'Translucent frosted glass with gold glow',
      icon: '💎',
    },
    {
      id: 'darkGold',
      title: 'Obsidian Gold 🌙',
      subtitle: 'Deep luxury dark with metallic highlights',
      icon: '👑',
    },
    {
      id: 'light',
      title: 'Minimal Champagne ☀️',
      subtitle: 'Crisp light interface with warm golden accents',
      icon: '✨',
    },
  ];

  return (
    <ScrollView style={[styles.container, { backgroundColor: theme.background }]}>
      <View style={styles.content}>
        <Text style={[styles.headerTitle, { color: theme.textPrimary }]}>App Configuration</Text>
        <Text style={[styles.headerSubtitle, { color: theme.textMuted }]}>
          Manage local server IP connection & active visual theme
        </Text>

        {/* Server Connection GlassCard */}
        <GlassCard style={styles.card}>
          <Text style={[styles.cardTitle, { color: theme.accent }]}>📡 Local Server Connection</Text>
          <Text style={[styles.cardDesc, { color: theme.textMuted }]}>
            Enter the IP address & port of your ConceptJewelleryPro ERP Server (e.g. http://192.168.1.100:5000)
          </Text>

          <GlassInput
            label="Server IP / Host URL"
            value={serverUrl}
            onChangeText={setServerUrl}
            placeholder="http://192.168.1.100:5000"
            autoCapitalize="none"
            autoCorrect={false}
          />

          {testResult && (
            <View
              style={[
                styles.resultBox,
                {
                  backgroundColor: testResult.success ? 'rgba(16, 185, 129, 0.12)' : 'rgba(239, 68, 68, 0.12)',
                  borderColor: testResult.success ? theme.statusSuccess : theme.statusError,
                },
              ]}
            >
              <Text
                style={[
                  styles.resultText,
                  { color: testResult.success ? theme.statusSuccess : theme.statusError },
                ]}
              >
                {testResult.success ? '✅ ' : '❌ '}
                {testResult.message}
              </Text>
            </View>
          )}

          <View style={styles.btnRow}>
            <GlassButton
              title="Test Connection"
              variant="secondary"
              onPress={handleTest}
              loading={testing}
              style={{ flex: 1 }}
            />
            <GlassButton title="Save Server URL" onPress={handleSave} style={{ flex: 1 }} />
          </View>
        </GlassCard>

        {/* Theme Selector GlassCard */}
        <GlassCard style={styles.card}>
          <Text style={[styles.cardTitle, { color: theme.accent }]}>🎨 Visual Theme</Text>
          <Text style={[styles.cardDesc, { color: theme.textMuted }]}>
            Select your preferred showroom aesthetic
          </Text>

          {themeOptions.map((opt) => {
            const isSelected = themeMode === opt.id;
            return (
              <TouchableOpacity
                key={opt.id}
                activeOpacity={0.8}
                onPress={() => setThemeMode(opt.id)}
                style={[
                  styles.themeOption,
                  {
                    backgroundColor: isSelected ? 'rgba(212, 175, 55, 0.15)' : theme.inputBackground,
                    borderColor: isSelected ? theme.accent : theme.inputBorder,
                    borderWidth: isSelected ? 2 : 1,
                  },
                ]}
              >
                <Text style={{ fontSize: 24 }}>{opt.icon}</Text>
                <View style={{ flex: 1, marginLeft: 12 }}>
                  <Text style={[styles.themeTitle, { color: theme.textPrimary }]}>{opt.title}</Text>
                  <Text style={[styles.themeSubtitle, { color: theme.textMuted }]}>{opt.subtitle}</Text>
                </View>
                {isSelected && <Text style={[styles.checkMark, { color: theme.accent }]}>✓</Text>}
              </TouchableOpacity>
            );
          })}
        </GlassCard>

        {/* Staff Profile / Account details */}
        {user && (
          <GlassCard style={styles.card}>
            <Text style={[styles.cardTitle, { color: theme.accent }]}>👤 Staff Profile</Text>
            <View style={styles.infoRow}>
              <Text style={[styles.infoLabel, { color: theme.textMuted }]}>Logged In As:</Text>
              <Text style={[styles.infoValue, { color: theme.textPrimary }]}>{user.username}</Text>
            </View>
            <View style={styles.infoRow}>
              <Text style={[styles.infoLabel, { color: theme.textMuted }]}>Role:</Text>
              <Text style={[styles.infoValue, { color: theme.accent }]}>{user.role}</Text>
            </View>

            <GlassButton title="Log Out" variant="danger" onPress={logout} style={{ marginTop: 14 }} />
          </GlassCard>
        )}
      </View>
    </ScrollView>
  );
};

const styles = StyleSheet.create({
  container: { flex: 1 },
  content: { padding: 20, paddingTop: 60, paddingBottom: 40 },
  headerTitle: { fontSize: 24, fontWeight: '800', letterSpacing: 0.5 },
  headerSubtitle: { fontSize: 13, marginTop: 4, marginBottom: 20 },
  card: { marginBottom: 20 },
  cardTitle: { fontSize: 16, fontWeight: '700', marginBottom: 4 },
  cardDesc: { fontSize: 12, marginBottom: 14, lineHeight: 18 },
  resultBox: { padding: 12, borderRadius: 10, borderWidth: 1, marginBottom: 14 },
  resultText: { fontSize: 13, fontWeight: '600' },
  btnRow: { flexDirection: 'row', gap: 10 },
  themeOption: {
    flexDirection: 'row',
    alignItems: 'center',
    padding: 14,
    borderRadius: 14,
    marginBottom: 10,
  },
  themeTitle: { fontSize: 15, fontWeight: '700' },
  themeSubtitle: { fontSize: 12, marginTop: 2 },
  checkMark: { fontSize: 18, fontWeight: '900' },
  infoRow: { flexDirection: 'row', justifyContent: 'space-between', paddingVertical: 6 },
  infoLabel: { fontSize: 13 },
  infoValue: { fontSize: 14, fontWeight: '700' },
});
