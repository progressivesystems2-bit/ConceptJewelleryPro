import React from 'react';
import {
  View,
  Text,
  StyleSheet,
  ScrollView,
  TouchableOpacity,
  Dimensions,
} from 'react-native';
import { useTheme } from '../theme/ThemeContext';
import { useAuth } from '../context/AuthContext';
import { GlassCard } from '../components/GlassCard';

const { width } = Dimensions.get('window');
const GRID_GAP = 12;
const CARD_WIDTH = (width - 40 - GRID_GAP * 2) / 3;

export interface DashboardScreenProps {
  onNavigate: (screenName: string) => void;
}

export const DashboardScreen: React.FC<DashboardScreenProps> = ({ onNavigate }) => {
  const { theme, themeMode } = useTheme();
  const { user, logout } = useAuth();

  const currentThemeLabel = themeMode || 'glassmorphism';

  const gridItems = [
    {
      id: 'Valuation',
      title: 'Barcode\nValuation',
      icon: '🏷️',
      badge: 'Live Rates',
      color: '#E5C158',
    },
    {
      id: 'GenerateInvoice',
      title: 'Generate\nInvoice',
      icon: '🧾',
      badge: 'Billing',
      color: '#4ADE80',
    },
    {
      id: 'CustomerReq',
      title: 'Customer\nRequirement',
      icon: '📝',
      badge: 'Orders',
      color: '#60A5FA',
    },
    {
      id: 'Catalogue',
      title: 'Stock\nCatalogue',
      icon: '💎',
      badge: 'Showcase',
      color: '#F472B6',
    },
    {
      id: 'BarcodeImageUpload',
      title: 'Barcode Image\n& Sticker',
      icon: '📸',
      badge: 'Capture',
      color: '#F59E0B',
    },
    {
      id: 'DocUpload',
      title: 'Misc Data\nUpload',
      icon: '📁',
      badge: 'KYC/Docs',
      color: '#A78BFA',
    },
    {
      id: 'ValuationRates',
      title: 'Live Metal\nRates',
      icon: '📊',
      badge: '22K / 18K',
      color: '#FBBF24',
    },
    {
      id: 'CatalogueStock',
      title: 'Stock\nSummary',
      icon: '📦',
      badge: 'Real-time',
      color: '#34D399',
    },
    {
      id: 'Config',
      title: 'Server &\nSettings',
      icon: '⚙️',
      badge: currentThemeLabel.toUpperCase(),
      color: '#9CA3AF',
    },
  ];

  const handleTilePress = (id: string) => {
    if (id === 'ValuationRates') {
      onNavigate('Valuation');
    } else if (id === 'CatalogueStock') {
      onNavigate('Catalogue');
    } else {
      onNavigate(id);
    }
  };

  return (
    <ScrollView
      style={[styles.container, { backgroundColor: theme.background }]}
      contentContainerStyle={styles.scrollContent}
      showsVerticalScrollIndicator={false}
    >
      {/* Staff Welcome Banner */}
      <GlassCard style={styles.welcomeCard} glow>
        <View style={styles.welcomeRow}>
          <View style={{ flex: 1 }}>
            <Text style={[styles.welcomeGreeting, { color: theme.textMuted }]}>
              WELCOME BACK
            </Text>
            <Text style={[styles.staffName, { color: theme.textPrimary }]}>
              {user?.username ? user.username.toUpperCase() : 'STAFF USER'}
            </Text>
            <Text style={[styles.staffRole, { color: theme.accent }]}>
              {user?.role || 'Administrator'} • Showroom Portal
            </Text>
          </View>
          <TouchableOpacity
            activeOpacity={0.7}
            onPress={() => onNavigate('Config')}
            style={[styles.themePill, { borderColor: theme.cardBorder, backgroundColor: theme.inputBackground }]}
          >
            <Text style={[styles.themePillText, { color: theme.accent }]}>
              ✨ {currentThemeLabel}
            </Text>
          </TouchableOpacity>
        </View>
      </GlassCard>

      {/* Section Header */}
      <View style={styles.sectionHeader}>
        <Text style={[styles.sectionTitle, { color: theme.textPrimary }]}>
          Showroom Operations
        </Text>
        <Text style={[styles.sectionSubtitle, { color: theme.textMuted }]}>
          Select a business module to continue
        </Text>
      </View>

      {/* 3x3 Grid */}
      <View style={styles.gridContainer}>
        {gridItems.map((item) => (
          <TouchableOpacity
            key={item.id}
            activeOpacity={0.8}
            onPress={() => handleTilePress(item.id)}
            style={styles.gridItemWrapper}
          >
            <GlassCard
              style={[
                styles.gridTile,
                { width: CARD_WIDTH, minHeight: CARD_WIDTH * 1.12 },
              ]}
            >
              <View style={styles.iconCircle}>
                <Text style={styles.iconText}>{item.icon}</Text>
              </View>

              <Text
                style={[styles.tileTitle, { color: theme.textPrimary }]}
                numberOfLines={2}
              >
                {item.title}
              </Text>

              <View
                style={[
                  styles.badgeContainer,
                  { backgroundColor: theme.inputBackground, borderColor: theme.cardBorder },
                ]}
              >
                <Text
                  style={[styles.badgeText, { color: theme.accent }]}
                  numberOfLines={1}
                >
                  {item.badge}
                </Text>
              </View>
            </GlassCard>
          </TouchableOpacity>
        ))}
      </View>

      {/* Quick Summary Strip */}
      <GlassCard style={styles.quickBar}>
        <View style={styles.quickStat}>
          <Text style={[styles.quickStatLabel, { color: theme.textMuted }]}>22K Gold</Text>
          <Text style={[styles.quickStatVal, { color: theme.accent }]}>₹6,850/g</Text>
        </View>
        <View style={[styles.statDivider, { backgroundColor: theme.cardBorder }]} />
        <View style={styles.quickStat}>
          <Text style={[styles.quickStatLabel, { color: theme.textMuted }]}>18K Gold</Text>
          <Text style={[styles.quickStatVal, { color: theme.textPrimary }]}>₹5,600/g</Text>
        </View>
        <View style={[styles.statDivider, { backgroundColor: theme.cardBorder }]} />
        <View style={styles.quickStat}>
          <Text style={[styles.quickStatLabel, { color: theme.textMuted }]}>Silver</Text>
          <Text style={[styles.quickStatVal, { color: theme.textPrimary }]}>₹88/g</Text>
        </View>
      </GlassCard>
    </ScrollView>
  );
};

const styles = StyleSheet.create({
  container: { flex: 1 },
  scrollContent: { padding: 18, paddingBottom: 40 },
  welcomeCard: {
    padding: 16,
    marginBottom: 16,
    borderRadius: 16,
  },
  welcomeRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
  },
  welcomeGreeting: {
    fontSize: 10,
    fontWeight: '800',
    letterSpacing: 1.2,
  },
  staffName: {
    fontSize: 20,
    fontWeight: '900',
    marginTop: 2,
  },
  staffRole: {
    fontSize: 12,
    fontWeight: '700',
    marginTop: 2,
  },
  themePill: {
    paddingHorizontal: 10,
    paddingVertical: 6,
    borderRadius: 20,
    borderWidth: 1,
  },
  themePillText: {
    fontSize: 11,
    fontWeight: '800',
    textTransform: 'capitalize',
  },
  sectionHeader: {
    marginBottom: 14,
  },
  sectionTitle: {
    fontSize: 17,
    fontWeight: '800',
    letterSpacing: 0.2,
  },
  sectionSubtitle: {
    fontSize: 12,
    marginTop: 2,
  },
  gridContainer: {
    flexDirection: 'row',
    flexWrap: 'wrap',
    gap: GRID_GAP,
    justifyContent: 'space-between',
    marginBottom: 16,
  },
  gridItemWrapper: {
    marginBottom: 2,
  },
  gridTile: {
    alignItems: 'center',
    justifyContent: 'space-between',
    padding: 10,
    paddingVertical: 12,
    borderRadius: 14,
  },
  iconCircle: {
    width: 44,
    height: 44,
    borderRadius: 22,
    alignItems: 'center',
    justifyContent: 'center',
    marginBottom: 6,
  },
  iconText: {
    fontSize: 24,
  },
  tileTitle: {
    fontSize: 11,
    fontWeight: '800',
    textAlign: 'center',
    lineHeight: 14,
    minHeight: 28,
  },
  badgeContainer: {
    marginTop: 6,
    paddingHorizontal: 6,
    paddingVertical: 2,
    borderRadius: 6,
    borderWidth: 1,
    maxWidth: '100%',
  },
  badgeText: {
    fontSize: 9,
    fontWeight: '800',
    textAlign: 'center',
  },
  quickBar: {
    flexDirection: 'row',
    justifyContent: 'space-around',
    alignItems: 'center',
    paddingVertical: 12,
    borderRadius: 14,
  },
  quickStat: {
    alignItems: 'center',
  },
  quickStatLabel: {
    fontSize: 10,
    fontWeight: '700',
    textTransform: 'uppercase',
  },
  quickStatVal: {
    fontSize: 14,
    fontWeight: '900',
    marginTop: 2,
  },
  statDivider: {
    width: 1,
    height: 24,
  },
});
