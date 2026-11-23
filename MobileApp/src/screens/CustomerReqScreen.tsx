import React, { useState } from 'react';
import { View, Text, StyleSheet, ScrollView, Alert } from 'react-native';
import { useTheme } from '../theme/ThemeContext';
import { getApiClient } from '../config/apiService';
import { GlassCard } from '../components/GlassCard';
import { GlassInput } from '../components/GlassInput';
import { GlassButton } from '../components/GlassButton';

export const CustomerReqScreen: React.FC = () => {
  const { theme } = useTheme();

  const [customerName, setCustomerName] = useState('');
  const [phone, setPhone] = useState('');
  const [itemName, setItemName] = useState('');
  const [targetPurity, setTargetPurity] = useState('22K');
  const [targetWeight, setTargetWeight] = useState('');
  const [notes, setNotes] = useState('');
  const [loading, setLoading] = useState(false);

  const handleSubmit = async () => {
    if (!customerName.trim() || !phone.trim() || !itemName.trim()) {
      Alert.alert('Required Fields', 'Please fill customer name, phone number, and required item.');
      return;
    }

    setLoading(true);
    try {
      const client = await getApiClient();
      const payload = {
        customerName: customerName.trim(),
        phone: phone.trim(),
        itemName: itemName.trim(),
        purity: targetPurity,
        targetWeight: targetWeight.trim() ? parseFloat(targetWeight) : undefined,
        notes: notes.trim(),
      };

      await client.post('/api/CustomerRequirement', payload);
      Alert.alert('Requirement Saved', 'Customer requirement logged successfully into ERP system!');

      // Reset form
      setCustomerName('');
      setPhone('');
      setItemName('');
      setTargetWeight('');
      setNotes('');
    } catch (error: any) {
      const msg = error.response?.data?.message || error.message || 'Failed to save requirement';
      Alert.alert('Save Error', msg);
    } finally {
      setLoading(false);
    }
  };

  return (
    <ScrollView style={[styles.container, { backgroundColor: theme.background }]}>
      <View style={styles.content}>
        <Text style={[styles.title, { color: theme.textPrimary }]}>Customer Requirement Lead</Text>
        <Text style={[styles.subtitle, { color: theme.textMuted }]}>
          Log custom jewellery requests and showroom wishlist inquiries
        </Text>

        <GlassCard style={styles.card}>
          <GlassInput
            label="Customer Name *"
            value={customerName}
            onChangeText={setCustomerName}
            placeholder="e.g. Rahul Sharma"
          />

          <GlassInput
            label="Phone Number *"
            value={phone}
            onChangeText={setPhone}
            placeholder="e.g. +91 9876543210"
            keyboardType="phone-pad"
          />

          <GlassInput
            label="Item Description / Requested Article *"
            value={itemName}
            onChangeText={setItemName}
            placeholder="e.g. Antique Temple Necklace or Diamond Studs"
          />

          <View style={styles.row}>
            <View style={{ flex: 1 }}>
              <GlassInput
                label="Target Purity"
                value={targetPurity}
                onChangeText={setTargetPurity}
                placeholder="22K / 18K / 24K"
              />
            </View>

            <View style={{ flex: 1 }}>
              <GlassInput
                label="Target Wt (g)"
                value={targetWeight}
                onChangeText={setTargetWeight}
                placeholder="e.g. 25.0"
                keyboardType="numeric"
              />
            </View>
          </View>

          <GlassInput
            label="Design Customization Notes"
            value={notes}
            onChangeText={setNotes}
            placeholder="Specific gemstone requirements, hook style, or delivery deadline..."
            multiline
            numberOfLines={3}
          />

          <GlassButton
            title="Log Requirement into ERP"
            onPress={handleSubmit}
            loading={loading}
            style={{ marginTop: 14 }}
          />
        </GlassCard>
      </View>
    </ScrollView>
  );
};

const styles = StyleSheet.create({
  container: { flex: 1 },
  content: { padding: 20, paddingTop: 20, paddingBottom: 40 },
  title: { fontSize: 22, fontWeight: '800' },
  subtitle: { fontSize: 12, marginTop: 4, marginBottom: 20, lineHeight: 18 },
  card: { marginBottom: 20 },
  row: { flexDirection: 'row', gap: 10 },
});
