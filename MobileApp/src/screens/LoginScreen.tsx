import React, { useState } from 'react';
import { View, Text, StyleSheet, KeyboardAvoidingView, Platform, ScrollView } from 'react-native';
import { useTheme } from '../theme/ThemeContext';
import { useAuth } from '../context/AuthContext';
import { GlassCard } from '../components/GlassCard';
import { GlassInput } from '../components/GlassInput';
import { GlassButton } from '../components/GlassButton';

interface LoginScreenProps {
  onOpenConfig: () => void;
}

export const LoginScreen: React.FC<LoginScreenProps> = ({ onOpenConfig }) => {
  const { theme } = useTheme();
  const { login } = useAuth();

  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');
  const [loading, setLoading] = useState(false);
  const [errorMsg, setErrorMsg] = useState<string | null>(null);

  const handleLogin = async () => {
    if (!username.trim() || !password.trim()) {
      setErrorMsg('Please enter both username and password.');
      return;
    }

    setLoading(true);
    setErrorMsg(null);

    const res = await login(username.trim(), password.trim());
    setLoading(false);

    if (!res.success) {
      setErrorMsg(res.message || 'Login failed. Check username, password, or server connection.');
    }
  };

  return (
    <KeyboardAvoidingView
      behavior={Platform.OS === 'ios' ? 'padding' : 'height'}
      style={[styles.container, { backgroundColor: theme.background }]}
    >
      <ScrollView contentContainerStyle={styles.scrollContent}>
        <View style={styles.logoContainer}>
          <Text style={{ fontSize: 44, marginBottom: 8 }}>💎</Text>
          <Text style={[styles.title, { color: theme.textPrimary }]}>Concept Jewellery Pro</Text>
          <Text style={[styles.subtitle, { color: theme.accent }]}>MOBILE ERP PORTAL</Text>
        </View>

        <GlassCard style={styles.card} glow>
          <Text style={[styles.cardTitle, { color: theme.textPrimary }]}>Staff Sign In</Text>

          {errorMsg && (
            <View style={[styles.errorBox, { backgroundColor: 'rgba(239, 68, 68, 0.15)' }]}>
              <Text style={[styles.errorText, { color: theme.statusError }]}>{errorMsg}</Text>
            </View>
          )}

          <GlassInput
            label="Username"
            value={username}
            onChangeText={setUsername}
            placeholder="e.g. admin or sales1"
            autoCapitalize="none"
          />

          <GlassInput
            label="Password"
            value={password}
            onChangeText={setPassword}
            placeholder="••••••••"
            secureTextEntry
          />

          <GlassButton
            title="Authenticate"
            onPress={handleLogin}
            loading={loading}
            style={{ marginTop: 10 }}
          />
        </GlassCard>

        <View style={styles.footer}>
          <GlassButton
            title="⚙️ Configure Server Connection"
            variant="secondary"
            onPress={onOpenConfig}
          />
        </View>
      </ScrollView>
    </KeyboardAvoidingView>
  );
};

const styles = StyleSheet.create({
  container: { flex: 1 },
  scrollContent: {
    flexGrow: 1,
    justifyContent: 'center',
    padding: 24,
  },
  logoContainer: {
    alignItems: 'center',
    marginBottom: 32,
  },
  title: {
    fontSize: 24,
    fontWeight: '900',
    letterSpacing: 0.5,
    textAlign: 'center',
  },
  subtitle: {
    fontSize: 12,
    fontWeight: '800',
    letterSpacing: 2,
    marginTop: 4,
  },
  card: {
    padding: 24,
  },
  cardTitle: {
    fontSize: 18,
    fontWeight: '800',
    marginBottom: 20,
    textAlign: 'center',
  },
  errorBox: {
    padding: 12,
    borderRadius: 10,
    marginBottom: 16,
  },
  errorText: {
    fontSize: 13,
    fontWeight: '600',
    textAlign: 'center',
  },
  footer: {
    marginTop: 24,
  },
});
