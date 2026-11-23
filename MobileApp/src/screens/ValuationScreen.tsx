import React, { useState, useEffect } from 'react';
import {
  View,
  Text,
  StyleSheet,
  ScrollView,
  TouchableOpacity,
  Image,
  Alert,
  ActivityIndicator,
} from 'react-native';
import { useTheme } from '../theme/ThemeContext';
import { getApiClient, getApiBaseUrl } from '../config/apiService';
import { GlassCard } from '../components/GlassCard';
import { GlassInput } from '../components/GlassInput';
import { GlassButton } from '../components/GlassButton';
import { BarcodeScannerModal } from '../components/BarcodeScannerModal';

export interface BarcodeDetails {
  barcode: string;
  itemName: string;
  itemCode: string;
  purity: string;
  grossWt: number;
  stoneWt: number;
  netWt: number;
  making: number;
  makingType: string;
  mPerGr: number;
  stoneValue: number;
  mrp: number;
  size: string;
  description: string;
  huid: string;
  ratePerGram: number;
  imageUrl?: string | null;
}

export interface ValuationResult {
  barcode: string;
  itemName: string;
  purity: string;
  grossWt: number;
  stoneWt: number;
  netWt: number;
  ratePerGram: number;
  metalValue: number;
  stoneValue: number;
  makingAmount: number;
  subTotal: number;
  gstMode: string;
  gstPercent: number;
  gstAmount: number;
  finalPrice: number;
}

export const ValuationScreen: React.FC<{ onBack?: () => void }> = ({ onBack }) => {
  const { theme } = useTheme();

  const [barcode, setBarcode] = useState('');
  const [product, setProduct] = useState<BarcodeDetails | null>(null);
  const [fetchingProduct, setFetchingProduct] = useState(false);
  const [baseUrl, setBaseUrl] = useState('');

  const [makingPerGram, setMakingPerGram] = useState('');
  const [makingPercent, setMakingPercent] = useState('');
  const [gstMode, setGstMode] = useState<'ExclusiveGST' | 'InclusiveGST'>('ExclusiveGST');

  const [loading, setLoading] = useState(false);
  const [result, setResult] = useState<ValuationResult | null>(null);
  const [scannerVisible, setScannerVisible] = useState(false);

  useEffect(() => {
    getApiBaseUrl().then((url) => setBaseUrl(url));
  }, []);

  // Fetch product info & live purity rate whenever barcode changes
  const handleFetchProduct = async (code: string) => {
    if (!code.trim()) return;

    setFetchingProduct(true);
    setResult(null);

    try {
      const client = await getApiClient();
      const response = await client.get(`/api/Barcode/${code.trim()}`);
      const data: BarcodeDetails = response.data;
      setProduct(data);

      // Auto-load making charges from database
      if (data.makingType === 'PERCENT' && data.making > 0) {
        setMakingPercent(data.making.toString());
        setMakingPerGram('');
      } else if (data.mPerGr > 0) {
        setMakingPerGram(data.mPerGr.toString());
        setMakingPercent('');
      } else if (data.making > 0) {
        setMakingPerGram(data.making.toString());
        setMakingPercent('');
      } else {
        setMakingPerGram('');
        setMakingPercent('');
      }
    } catch (error: any) {
      const msg = error.response?.data?.message || 'Barcode not found in database.';
      Alert.alert('Barcode Lookup', msg);
      setProduct(null);
    } finally {
      setFetchingProduct(false);
    }
  };

  const handleCalculate = async () => {
    const codeToUse = barcode.trim();
    if (!codeToUse) {
      Alert.alert('Input Required', 'Please enter or scan a barcode.');
      return;
    }

    setLoading(true);

    try {
      const client = await getApiClient();
      const payload: any = {
        barcode: codeToUse,
        gstMode,
      };

      if (makingPerGram.trim()) payload.makingPerGram = parseFloat(makingPerGram);
      if (makingPercent.trim()) payload.makingPercent = parseFloat(makingPercent);

      const response = await client.post('/api/Valuation', payload);
      setResult(response.data);
    } catch (error: any) {
      const msg = error.response?.data?.message || error.message || 'Failed to calculate valuation';
      Alert.alert('Valuation Error', msg);
    } finally {
      setLoading(false);
    }
  };

  const handleScanned = (scannedCode: string) => {
    setBarcode(scannedCode);
    handleFetchProduct(scannedCode);
  };

  const fullImageUrl = product?.imageUrl
    ? product.imageUrl.startsWith('http')
      ? product.imageUrl
      : `${baseUrl}${product.imageUrl}`
    : null;

  return (
    <ScrollView
      style={[styles.container, { backgroundColor: theme.background }]}
      contentContainerStyle={styles.scrollContent}
      showsVerticalScrollIndicator={false}
    >
      {/* Screen Header */}
      <View style={styles.headerRow}>
        <View style={{ flex: 1 }}>
          <Text style={[styles.title, { color: theme.textPrimary }]}>Barcode Valuation</Text>
          <Text style={[styles.subtitle, { color: theme.textMuted }]}>
            Scan tag to auto-load specs, metal rates & calculate live price
          </Text>
        </View>
      </View>

      {/* Barcode Search Card */}
      <GlassCard style={styles.card}>
        <View style={styles.inputRow}>
          <View style={{ flex: 1 }}>
            <GlassInput
              label="Article Barcode"
              value={barcode}
              onChangeText={setBarcode}
              placeholder="e.g. BC1002 or 000001"
              autoCapitalize="characters"
              onSubmitEditing={() => handleFetchProduct(barcode)}
            />
          </View>

          <TouchableOpacity
            activeOpacity={0.8}
            onPress={() => setScannerVisible(true)}
            style={[
              styles.scanBtn,
              { backgroundColor: theme.accent, borderColor: theme.cardBorder },
            ]}
          >
            <Text style={{ fontSize: 20 }}>📷</Text>
          </TouchableOpacity>

          <TouchableOpacity
            activeOpacity={0.8}
            onPress={() => handleFetchProduct(barcode)}
            style={[
              styles.scanBtn,
              { backgroundColor: theme.cardBackground, borderColor: theme.cardBorder },
            ]}
          >
            {fetchingProduct ? (
              <ActivityIndicator size="small" color={theme.accent} />
            ) : (
              <Text style={{ fontSize: 18 }}>🔍</Text>
            )}
          </TouchableOpacity>
        </View>
      </GlassCard>

      {/* Product Details & Image Card */}
      {product && (
        <GlassCard style={styles.card} glow>
          <View style={styles.productRow}>
            {fullImageUrl ? (
              <Image
                source={{ uri: fullImageUrl }}
                style={[styles.productThumb, { borderColor: theme.cardBorder }]}
                resizeMode="cover"
              />
            ) : (
              <View
                style={[
                  styles.productPlaceholder,
                  { backgroundColor: theme.inputBackground, borderColor: theme.cardBorder },
                ]}
              >
                <Text style={{ fontSize: 32 }}>💎</Text>
              </View>
            )}

            <View style={{ flex: 1, marginLeft: 14 }}>
              <View style={styles.purityRow}>
                <Text style={[styles.purityBadge, { backgroundColor: theme.accent, color: theme.accentText }]}>
                  {product.purity || '22K'}
                </Text>
                {product.size && product.size !== '-' && (
                  <Text style={[styles.sizeBadge, { borderColor: theme.cardBorder, color: theme.textSecondary }]}>
                    Size: {product.size}
                  </Text>
                )}
              </View>

              <Text style={[styles.productName, { color: theme.textPrimary }]} numberOfLines={2}>
                {product.itemName}
              </Text>

              {product.description ? (
                <Text style={[styles.productDesc, { color: theme.textMuted }]} numberOfLines={1}>
                  {product.description}
                </Text>
              ) : null}

              {product.huid ? (
                <Text style={[styles.huidText, { color: theme.accent }]}>
                  HUID: {product.huid}
                </Text>
              ) : null}
            </View>
          </View>

          <View style={[styles.divider, { backgroundColor: theme.cardBorder }]} />

          {/* Weights & Live Rate Grid */}
          <View style={styles.specGrid}>
            <View style={styles.specBox}>
              <Text style={[styles.specLabel, { color: theme.textMuted }]}>Gross Wt</Text>
              <Text style={[styles.specValue, { color: theme.textPrimary }]}>{product.grossWt} g</Text>
            </View>
            <View style={styles.specBox}>
              <Text style={[styles.specLabel, { color: theme.textMuted }]}>Stone Wt</Text>
              <Text style={[styles.specValue, { color: theme.textPrimary }]}>{product.stoneWt} g</Text>
            </View>
            <View style={styles.specBox}>
              <Text style={[styles.specLabel, { color: theme.textMuted }]}>Net Wt</Text>
              <Text style={[styles.specValue, { color: theme.accent }]}>{product.netWt} g</Text>
            </View>
            <View style={styles.specBox}>
              <Text style={[styles.specLabel, { color: theme.textMuted }]}>Purity Rate</Text>
              <Text style={[styles.specValue, { color: theme.accent }]}>
                ₹{product.ratePerGram ? product.ratePerGram.toLocaleString() : '6,850'}/g
              </Text>
            </View>
          </View>
        </GlassCard>
      )}

      {/* Making Charges & GST Controls */}
      <GlassCard style={styles.card}>
        <Text style={[styles.sectionHeaderTitle, { color: theme.textPrimary }]}>
          Making Charges & Valuation Settings
        </Text>

        <View style={styles.makingRow}>
          <View style={{ flex: 1 }}>
            <GlassInput
              label="Making / Gram (₹)"
              value={makingPerGram}
              onChangeText={(val: string) => {
                setMakingPerGram(val);
                if (val) setMakingPercent('');
              }}
              placeholder="e.g. 450"
              keyboardType="numeric"
            />
          </View>
          <Text style={[styles.orText, { color: theme.textMuted }]}>OR</Text>
          <View style={{ flex: 1 }}>
            <GlassInput
              label="Making (%)"
              value={makingPercent}
              onChangeText={(val: string) => {
                setMakingPercent(val);
                if (val) setMakingPerGram('');
              }}
              placeholder="e.g. 12"
              keyboardType="numeric"
            />
          </View>
        </View>

        {/* GST Mode Toggle */}
        <Text style={[styles.label, { color: theme.textSecondary }]}>GST Calculation Mode</Text>
        <View style={styles.gstToggleRow}>
          <TouchableOpacity
            activeOpacity={0.8}
            onPress={() => setGstMode('ExclusiveGST')}
            style={[
              styles.gstTab,
              {
                backgroundColor: gstMode === 'ExclusiveGST' ? theme.accent : theme.inputBackground,
                borderColor: gstMode === 'ExclusiveGST' ? theme.accent : theme.inputBorder,
              },
            ]}
          >
            <Text
              style={[
                styles.gstTabText,
                { color: gstMode === 'ExclusiveGST' ? theme.accentText : theme.textPrimary },
              ]}
            >
              + 3% GST (Exclusive)
            </Text>
          </TouchableOpacity>

          <TouchableOpacity
            activeOpacity={0.8}
            onPress={() => setGstMode('InclusiveGST')}
            style={[
              styles.gstTab,
              {
                backgroundColor: gstMode === 'InclusiveGST' ? theme.accent : theme.inputBackground,
                borderColor: gstMode === 'InclusiveGST' ? theme.accent : theme.inputBorder,
              },
            ]}
          >
            <Text
              style={[
                styles.gstTabText,
                { color: gstMode === 'InclusiveGST' ? theme.accentText : theme.textPrimary },
              ]}
            >
              GST Included
            </Text>
          </TouchableOpacity>
        </View>

        <GlassButton
          title="Calculate Price"
          onPress={handleCalculate}
          loading={loading}
          style={{ marginTop: 14 }}
        />
      </GlassCard>

      {/* Valuation Result Receipt Card */}
      {result && (
        <GlassCard style={styles.card} glow>
          <View style={styles.resultHeader}>
            <View>
              <Text style={[styles.itemName, { color: theme.textPrimary }]}>{result.itemName}</Text>
              <Text style={[styles.resultPurity, { color: theme.accent }]}>
                {result.purity} Purity • Tag #{result.barcode}
              </Text>
            </View>
          </View>

          <View style={[styles.divider, { backgroundColor: theme.cardBorder }]} />

          {/* Breakdown Items */}
          <View style={styles.lineItem}>
            <Text style={[styles.lineLabel, { color: theme.textSecondary }]}>
              Metal Value ({result.netWt ?? 0}g × ₹{result.ratePerGram ?? 0})
            </Text>
            <Text style={[styles.lineVal, { color: theme.textPrimary }]}>
              ₹{(result.metalValue ?? 0).toLocaleString()}
            </Text>
          </View>

          {(result.stoneValue ?? 0) > 0 && (
            <View style={styles.lineItem}>
              <Text style={[styles.lineLabel, { color: theme.textSecondary }]}>
                Stone Value ({result.stoneWt ?? 0}g)
              </Text>
              <Text style={[styles.lineVal, { color: theme.textPrimary }]}>
                ₹{(result.stoneValue ?? 0).toLocaleString()}
              </Text>
            </View>
          )}

          <View style={styles.lineItem}>
            <Text style={[styles.lineLabel, { color: theme.textSecondary }]}>Making Charges</Text>
            <Text style={[styles.lineVal, { color: theme.textPrimary }]}>
              ₹{(result.makingAmount ?? 0).toLocaleString()}
            </Text>
          </View>

          <View style={styles.lineItem}>
            <Text style={[styles.lineLabel, { color: theme.textSecondary }]}>SubTotal</Text>
            <Text style={[styles.lineVal, { color: theme.textPrimary }]}>
              ₹{(result.subTotal ?? 0).toLocaleString()}
            </Text>
          </View>

          <View style={styles.lineItem}>
            <Text style={[styles.lineLabel, { color: theme.textMuted }]}>
              GST ({result.gstPercent ?? 0}%) [{result.gstMode || ''}]
            </Text>
            <Text style={[styles.lineVal, { color: theme.textMuted }]}>
              ₹{(result.gstAmount ?? 0).toLocaleString()}
            </Text>
          </View>

          <View style={[styles.divider, { backgroundColor: theme.accent }]} />

          {/* Total Price Highlight */}
          <View style={styles.totalRow}>
            <Text style={[styles.totalLabel, { color: theme.textPrimary }]}>ESTIMATED PRICE</Text>
            <Text style={[styles.totalPrice, { color: theme.accent }]}>
              ₹{(result.finalPrice ?? 0).toLocaleString()}
            </Text>
          </View>
        </GlassCard>
      )}

      <BarcodeScannerModal
        visible={scannerVisible}
        onClose={() => setScannerVisible(false)}
        onScan={handleScanned}
      />
    </ScrollView>
  );
};

const styles = StyleSheet.create({
  container: { flex: 1 },
  scrollContent: { padding: 18, paddingTop: 12, paddingBottom: 40 },
  headerRow: { marginBottom: 14 },
  title: { fontSize: 21, fontWeight: '900' },
  subtitle: { fontSize: 12, marginTop: 3, lineHeight: 17 },
  card: { marginBottom: 16, padding: 16 },
  inputRow: { flexDirection: 'row', gap: 8, alignItems: 'flex-end' },
  scanBtn: {
    width: 48,
    height: 48,
    borderRadius: 12,
    borderWidth: 1,
    alignItems: 'center',
    justifyContent: 'center',
    marginBottom: 16,
  },
  productRow: { flexDirection: 'row', alignItems: 'center' },
  productThumb: { width: 70, height: 70, borderRadius: 12, borderWidth: 1 },
  productPlaceholder: {
    width: 70,
    height: 70,
    borderRadius: 12,
    borderWidth: 1,
    alignItems: 'center',
    justifyContent: 'center',
  },
  purityRow: { flexDirection: 'row', gap: 6, alignItems: 'center', marginBottom: 4 },
  purityBadge: {
    fontSize: 10,
    fontWeight: '900',
    paddingHorizontal: 6,
    paddingVertical: 2,
    borderRadius: 4,
    overflow: 'hidden',
  },
  sizeBadge: {
    fontSize: 10,
    fontWeight: '700',
    paddingHorizontal: 6,
    paddingVertical: 2,
    borderRadius: 4,
    borderWidth: 1,
  },
  productName: { fontSize: 16, fontWeight: '800' },
  productDesc: { fontSize: 11, marginTop: 2 },
  huidText: { fontSize: 10, fontWeight: '700', marginTop: 2 },
  divider: { height: 1, marginVertical: 12 },
  specGrid: { flexDirection: 'row', justifyContent: 'space-between' },
  specBox: { alignItems: 'center' },
  specLabel: { fontSize: 10, fontWeight: '700', textTransform: 'uppercase' },
  specValue: { fontSize: 14, fontWeight: '800', marginTop: 3 },
  sectionHeaderTitle: { fontSize: 13, fontWeight: '800', marginBottom: 10, textTransform: 'uppercase' },
  makingRow: { flexDirection: 'row', alignItems: 'center', gap: 8 },
  orText: { fontSize: 10, fontWeight: '800', marginTop: 12 },
  label: { fontSize: 11, fontWeight: '700', marginBottom: 8, textTransform: 'uppercase' },
  gstToggleRow: { flexDirection: 'row', gap: 10, marginBottom: 8 },
  gstTab: {
    flex: 1,
    paddingVertical: 10,
    borderRadius: 10,
    borderWidth: 1,
    alignItems: 'center',
  },
  gstTabText: { fontSize: 12, fontWeight: '700' },
  resultHeader: { flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center' },
  itemName: { fontSize: 18, fontWeight: '900' },
  resultPurity: { fontSize: 12, fontWeight: '700', marginTop: 2 },
  lineItem: { flexDirection: 'row', justifyContent: 'space-between', paddingVertical: 4 },
  lineLabel: { fontSize: 13 },
  lineVal: { fontSize: 13, fontWeight: '700' },
  totalRow: { flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center', paddingTop: 6 },
  totalLabel: { fontSize: 14, fontWeight: '900', letterSpacing: 0.5 },
  totalPrice: { fontSize: 24, fontWeight: '900' },
});
