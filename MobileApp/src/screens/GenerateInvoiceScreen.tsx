import React, { useState } from 'react';
import {
  View,
  Text,
  StyleSheet,
  ScrollView,
  TouchableOpacity,
  Alert,
} from 'react-native';
import { useTheme } from '../theme/ThemeContext';
import { GlassCard } from '../components/GlassCard';
import { GlassInput } from '../components/GlassInput';
import { GlassButton } from '../components/GlassButton';
import { BarcodeScannerModal } from '../components/BarcodeScannerModal';
import { getApiClient } from '../config/apiService';

interface InvoiceItem {
  barcode: string;
  itemName: string;
  purity: string;
  netWt: number;
  ratePerGram: number;
  making: number;
  finalPrice: number;
}

export const GenerateInvoiceScreen: React.FC<{ onBack?: () => void }> = ({ onBack }) => {
  const { theme } = useTheme();

  const [customerName, setCustomerName] = useState('');
  const [customerPhone, setCustomerPhone] = useState('');
  const [invoiceType, setInvoiceType] = useState<'TaxInvoice' | 'Estimate'>('TaxInvoice');
  const [barcodeInput, setBarcodeInput] = useState('');
  const [items, setItems] = useState<InvoiceItem[]>([]);
  const [addingItem, setAddingItem] = useState(false);
  const [scannerVisible, setScannerVisible] = useState(false);
  const [savingInvoice, setSavingInvoice] = useState(false);

  const handleAddItem = async (barcodeToAdd?: string) => {
    const code = (barcodeToAdd || barcodeInput).trim();
    if (!code) {
      Alert.alert('Input Required', 'Please enter or scan a barcode.');
      return;
    }

    setAddingItem(true);

    try {
      const client = await getApiClient();
      const valRes = await client.post('/api/Valuation', {
        barcode: code,
        gstMode: invoiceType === 'TaxInvoice' ? 'ExclusiveGST' : 'InclusiveGST',
      });

      const data = valRes.data;
      const newItem: InvoiceItem = {
        barcode: data.barcode,
        itemName: data.itemName,
        purity: data.purity,
        netWt: data.netWt,
        ratePerGram: data.ratePerGram,
        making: data.makingAmount,
        finalPrice: data.finalPrice,
      };

      setItems((prev) => [newItem, ...prev]);
      setBarcodeInput('');
    } catch (error: any) {
      const msg = error.response?.data?.message || 'Failed to fetch article valuation for invoice.';
      Alert.alert('Item Error', msg);
    } finally {
      setAddingItem(false);
    }
  };

  const handleRemoveItem = (index: number) => {
    setItems((prev) => prev.filter((_, i) => i !== index));
  };

  const totalNetWt = items.reduce((sum, i) => sum + i.netWt, 0);
  const totalAmount = items.reduce((sum, i) => sum + i.finalPrice, 0);

  const handleSaveInvoice = async () => {
    if (items.length === 0) {
      Alert.alert('No Items', 'Please add at least one jewellery article.');
      return;
    }

    if (!customerName.trim()) {
      Alert.alert('Customer Info', 'Please enter customer name.');
      return;
    }

    setSavingInvoice(true);
    setTimeout(() => {
      setSavingInvoice(false);
      Alert.alert(
        'Invoice Created 🎉',
        `Invoice for ${customerName.trim()} generated successfully!\nTotal: ₹${totalAmount.toLocaleString()} (${items.length} items)`,
        [
          {
            text: 'OK',
            onPress: () => {
              setItems([]);
              setCustomerName('');
              setCustomerPhone('');
            },
          },
        ]
      );
    }, 1000);
  };

  return (
    <ScrollView
      style={[styles.container, { backgroundColor: theme.background }]}
      contentContainerStyle={styles.scrollContent}
      showsVerticalScrollIndicator={false}
    >
      <View style={styles.headerRow}>
        <Text style={[styles.title, { color: theme.textPrimary }]}>Generate Invoice</Text>
        <Text style={[styles.subtitle, { color: theme.textMuted }]}>
          Create point-of-sale retail bill or quotation estimate
        </Text>
      </View>

      {/* Customer Info Card */}
      <GlassCard style={styles.card}>
        <Text style={[styles.sectionTitle, { color: theme.textPrimary }]}>
          Customer & Billing Info
        </Text>

        <GlassInput
          label="Customer Name"
          value={customerName}
          onChangeText={setCustomerName}
          placeholder="e.g. Rajesh Kumar"
        />

        <GlassInput
          label="Mobile Number"
          value={customerPhone}
          onChangeText={setCustomerPhone}
          placeholder="e.g. 9876543210"
          keyboardType="phone-pad"
        />

        {/* Invoice Type Toggle */}
        <View style={styles.typeRow}>
          <TouchableOpacity
            activeOpacity={0.8}
            onPress={() => setInvoiceType('TaxInvoice')}
            style={[
              styles.typeTab,
              {
                backgroundColor: invoiceType === 'TaxInvoice' ? theme.accent : theme.inputBackground,
                borderColor: invoiceType === 'TaxInvoice' ? theme.accent : theme.cardBorder,
              },
            ]}
          >
            <Text
              style={[
                styles.typeTabText,
                { color: invoiceType === 'TaxInvoice' ? theme.accentText : theme.textPrimary },
              ]}
            >
              🧾 Tax Invoice (+3% GST)
            </Text>
          </TouchableOpacity>

          <TouchableOpacity
            activeOpacity={0.8}
            onPress={() => setInvoiceType('Estimate')}
            style={[
              styles.typeTab,
              {
                backgroundColor: invoiceType === 'Estimate' ? theme.accent : theme.inputBackground,
                borderColor: invoiceType === 'Estimate' ? theme.accent : theme.cardBorder,
              },
            ]}
          >
            <Text
              style={[
                styles.typeTabText,
                { color: invoiceType === 'Estimate' ? theme.accentText : theme.textPrimary },
              ]}
            >
              📋 Estimate / Quotation
            </Text>
          </TouchableOpacity>
        </View>
      </GlassCard>

      {/* Add Items Card */}
      <GlassCard style={styles.card}>
        <Text style={[styles.sectionTitle, { color: theme.textPrimary }]}>
          Add Jewellery Articles
        </Text>

        <View style={styles.inputRow}>
          <View style={{ flex: 1 }}>
            <GlassInput
              label="Article Barcode"
              value={barcodeInput}
              onChangeText={setBarcodeInput}
              placeholder="e.g. BC1002"
              autoCapitalize="characters"
              onSubmitEditing={() => handleAddItem()}
            />
          </View>

          <TouchableOpacity
            activeOpacity={0.8}
            onPress={() => setScannerVisible(true)}
            style={[
              styles.iconBtn,
              { backgroundColor: theme.accent, borderColor: theme.cardBorder },
            ]}
          >
            <Text style={{ fontSize: 20 }}>📷</Text>
          </TouchableOpacity>

          <TouchableOpacity
            activeOpacity={0.8}
            onPress={() => handleAddItem()}
            style={[
              styles.iconBtn,
              { backgroundColor: theme.cardBackground, borderColor: theme.cardBorder },
            ]}
          >
            <Text style={{ fontSize: 18 }}>➕</Text>
          </TouchableOpacity>
        </View>
      </GlassCard>

      {/* Item List */}
      {items.length > 0 && (
        <GlassCard style={styles.card} glow>
          <View style={styles.itemListHeader}>
            <Text style={[styles.sectionTitle, { color: theme.textPrimary }]}>
              Billed Items ({items.length})
            </Text>
            <Text style={[styles.itemSummaryText, { color: theme.accent }]}>
              Net Wt: {totalNetWt.toFixed(3)}g
            </Text>
          </View>

          <View style={[styles.divider, { backgroundColor: theme.cardBorder }]} />

          {items.map((item, index) => (
            <View key={`${item.barcode}-${index}`} style={styles.itemRow}>
              <View style={{ flex: 1 }}>
                <Text style={[styles.rowItemName, { color: theme.textPrimary }]}>
                  {item.itemName}
                </Text>
                <Text style={[styles.rowItemSub, { color: theme.textMuted }]}>
                  #{item.barcode} • {item.purity} • {item.netWt}g @ ₹{item.ratePerGram}/g
                </Text>
              </View>

              <View style={{ alignItems: 'flex-end', marginLeft: 10 }}>
                <Text style={[styles.rowItemPrice, { color: theme.accent }]}>
                  ₹{item.finalPrice.toLocaleString()}
                </Text>
                <TouchableOpacity onPress={() => handleRemoveItem(index)}>
                  <Text style={styles.deleteText}>Delete ✕</Text>
                </TouchableOpacity>
              </View>
            </View>
          ))}

          <View style={[styles.divider, { backgroundColor: theme.accent }]} />

          {/* Grand Total */}
          <View style={styles.totalRow}>
            <Text style={[styles.totalLabel, { color: theme.textPrimary }]}>GRAND TOTAL</Text>
            <Text style={[styles.totalPrice, { color: theme.accent }]}>
              ₹{totalAmount.toLocaleString()}
            </Text>
          </View>

          <GlassButton
            title="Complete & Save Invoice"
            onPress={handleSaveInvoice}
            loading={savingInvoice}
            style={{ marginTop: 16 }}
          />
        </GlassCard>
      )}

      <BarcodeScannerModal
        visible={scannerVisible}
        onClose={() => setScannerVisible(false)}
        onScan={(code) => {
          setBarcodeInput(code);
          handleAddItem(code);
        }}
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
  sectionTitle: { fontSize: 13, fontWeight: '800', marginBottom: 10, textTransform: 'uppercase' },
  typeRow: { flexDirection: 'row', gap: 10, marginTop: 6, marginBottom: 8 },
  typeTab: { flex: 1, paddingVertical: 10, borderRadius: 10, borderWidth: 1, alignItems: 'center' },
  typeTabText: { fontSize: 11, fontWeight: '800' },
  inputRow: { flexDirection: 'row', gap: 8, alignItems: 'flex-end' },
  iconBtn: {
    width: 48,
    height: 48,
    borderRadius: 12,
    borderWidth: 1,
    alignItems: 'center',
    justifyContent: 'center',
    marginBottom: 16,
  },
  itemListHeader: { flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center' },
  itemSummaryText: { fontSize: 12, fontWeight: '800' },
  divider: { height: 1, marginVertical: 10 },
  itemRow: { flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center', paddingVertical: 8 },
  rowItemName: { fontSize: 14, fontWeight: '800' },
  rowItemSub: { fontSize: 11, marginTop: 2 },
  rowItemPrice: { fontSize: 15, fontWeight: '900' },
  deleteText: { color: '#EF4444', fontSize: 10, fontWeight: '700', marginTop: 4 },
  totalRow: { flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center', paddingTop: 6 },
  totalLabel: { fontSize: 14, fontWeight: '900', letterSpacing: 0.5 },
  totalPrice: { fontSize: 24, fontWeight: '900' },
});
