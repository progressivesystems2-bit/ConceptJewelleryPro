import React, { useState } from 'react';
import { View, Text, StyleSheet, TouchableOpacity } from 'react-native';
import { useTheme } from '../theme/ThemeContext';
import { useAuth } from '../context/AuthContext';
import { HeaderBar } from '../components/HeaderBar';

import { LoginScreen } from '../screens/LoginScreen';
import { DashboardScreen } from '../screens/DashboardScreen';
import { ValuationScreen } from '../screens/ValuationScreen';
import { BarcodeImageUploadScreen } from '../screens/BarcodeImageUploadScreen';
import { GenerateInvoiceScreen } from '../screens/GenerateInvoiceScreen';
import { CatalogueScreen } from '../screens/CatalogueScreen';
import { DocUploadScreen } from '../screens/DocUploadScreen';
import { CustomerReqScreen } from '../screens/CustomerReqScreen';
import { ConfigScreen } from '../screens/ConfigScreen';

export const AppNavigator: React.FC = () => {
  const { theme } = useTheme();
  const { user } = useAuth();

  const [currentScreen, setCurrentScreen] = useState<string>('Dashboard');
  const [showConfigOnly, setShowConfigOnly] = useState<boolean>(false);

  // If unauthenticated user requests config (server setup before login)
  if (showConfigOnly) {
    return (
      <View style={{ flex: 1, backgroundColor: theme.background }}>
        <HeaderBar title="Server Settings" />
        <ConfigScreen />
        <TouchableOpacity
          onPress={() => setShowConfigOnly(false)}
          style={[styles.backToLoginBtn, { backgroundColor: theme.accent }]}
        >
          <Text style={[styles.backToLoginText, { color: theme.accentText }]}>
            ← Back to Login
          </Text>
        </TouchableOpacity>
      </View>
    );
  }

  // Not logged in -> Show Login
  if (!user) {
    return <LoginScreen onOpenConfig={() => setShowConfigOnly(true)} />;
  }

  // Header Title mapping
  const screenTitles: Record<string, string> = {
    Dashboard: 'Concept Jewellery Pro',
    Valuation: 'Barcode Valuation',
    BarcodeImageUpload: 'Barcode Image & Sticker',
    GenerateInvoice: 'Generate Invoice',
    CustomerReq: 'Customer Orders / Leads',
    Catalogue: 'Stock Catalogue',
    DocUpload: 'Misc Data & KYC Upload',
    Config: 'Settings & Server Config',
  };

  const isSubScreen = currentScreen !== 'Dashboard';

  return (
    <View style={{ flex: 1, backgroundColor: theme.background }}>
      {/* Top Header */}
      <HeaderBar
        title={screenTitles[currentScreen] || 'Concept Jewellery Pro'}
        onOpenConfig={isSubScreen ? undefined : () => setCurrentScreen('Config')}
      />

      {/* SubScreen Back Navigation Bar */}
      {isSubScreen && (
        <View style={[styles.subNavBar, { borderBottomColor: theme.cardBorder, backgroundColor: theme.cardBackground }]}>
          <TouchableOpacity
            activeOpacity={0.7}
            onPress={() => setCurrentScreen('Dashboard')}
            style={styles.backBtn}
          >
            <Text style={[styles.backBtnText, { color: theme.accent }]}>
              ← Back to Dashboard
            </Text>
          </TouchableOpacity>
        </View>
      )}

      {/* Dynamic Screen View */}
      <View style={{ flex: 1 }}>
        {currentScreen === 'Dashboard' && (
          <DashboardScreen onNavigate={(screen) => setCurrentScreen(screen)} />
        )}
        {currentScreen === 'Valuation' && (
          <ValuationScreen onBack={() => setCurrentScreen('Dashboard')} />
        )}
        {currentScreen === 'BarcodeImageUpload' && (
          <BarcodeImageUploadScreen onBack={() => setCurrentScreen('Dashboard')} />
        )}
        {currentScreen === 'GenerateInvoice' && (
          <GenerateInvoiceScreen onBack={() => setCurrentScreen('Dashboard')} />
        )}
        {currentScreen === 'CustomerReq' && <CustomerReqScreen />}
        {currentScreen === 'Catalogue' && <CatalogueScreen />}
        {currentScreen === 'DocUpload' && <DocUploadScreen />}
        {currentScreen === 'Config' && <ConfigScreen />}
      </View>
    </View>
  );
};

const styles = StyleSheet.create({
  backToLoginBtn: {
    paddingVertical: 14,
    marginHorizontal: 20,
    marginBottom: 20,
    borderRadius: 12,
    alignItems: 'center',
  },
  backToLoginText: {
    fontSize: 14,
    fontWeight: '800',
  },
  subNavBar: {
    flexDirection: 'row',
    alignItems: 'center',
    paddingHorizontal: 16,
    paddingVertical: 8,
    borderBottomWidth: 1,
  },
  backBtn: {
    paddingVertical: 4,
  },
  backBtnText: {
    fontSize: 13,
    fontWeight: '800',
  },
});
