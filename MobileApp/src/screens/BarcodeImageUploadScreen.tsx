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
import * as ImagePicker from 'expo-image-picker';
import { useTheme } from '../theme/ThemeContext';
import { getApiClient, getApiBaseUrl } from '../config/apiService';
import { GlassCard } from '../components/GlassCard';
import { GlassInput } from '../components/GlassInput';
import { GlassButton } from '../components/GlassButton';
import { BarcodeScannerModal } from '../components/BarcodeScannerModal';

export interface BarcodeProductInfo {
  barcode: string;
  itemName: string;
  itemCode: string;
  purity: string;
  grossWt: number;
  netWt: number;
  mrp: number;
  size: string;
  imageUrl?: string | null;
  printStickerNo?: number;
}

export const BarcodeImageUploadScreen: React.FC<{ onBack?: () => void }> = ({ onBack }) => {
  const { theme } = useTheme();

  const [barcode, setBarcode] = useState('');
  const [product, setProduct] = useState<BarcodeProductInfo | null>(null);
  const [fetching, setFetching] = useState(false);
  const [baseUrl, setBaseUrl] = useState('');

  // Image state
  const [selectedImage, setSelectedImage] = useState<any>(null);
  const [uploading, setUploading] = useState(false);

  // Advanced Tab states
  const [activeTab, setActiveTab] = useState<'upload' | 'advanced'>('upload');
  const [editMrp, setEditMrp] = useState('');
  const [editSize, setEditSize] = useState('');
  const [reprintSticker, setReprintSticker] = useState(false);
  const [savingAdvanced, setSavingAdvanced] = useState(false);

  const [scannerVisible, setScannerVisible] = useState(false);

  useEffect(() => {
    getApiBaseUrl().then((url) => setBaseUrl(url));
  }, []);

  const handleFetchBarcode = async (code: string) => {
    if (!code.trim()) return;

    setFetching(true);
    setSelectedImage(null);

    try {
      const client = await getApiClient();
      const response = await client.get(`/api/Barcode/${code.trim()}`);
      const data: BarcodeProductInfo = response.data;
      setProduct(data);
      setEditMrp(data.mrp ? data.mrp.toString() : '');
      setEditSize(data.size && data.size !== '-' ? data.size : '');
    } catch (error: any) {
      const msg = error.response?.data?.message || 'Barcode not found.';
      Alert.alert('Barcode Lookup', msg);
      setProduct(null);
    } finally {
      setFetching(false);
    }
  };

  const handlePickFromGallery = async () => {
    const { status } = await ImagePicker.requestMediaLibraryPermissionsAsync();
    if (status !== 'granted') {
      Alert.alert('Permission Denied', 'Camera roll permissions are required.');
      return;
    }

    const result = await ImagePicker.launchImageLibraryAsync({
      mediaTypes: ['images'],
      allowsEditing: true,
      aspect: [1, 1],
      quality: 0.8,
    });

    if (!result.canceled && result.assets.length > 0) {
      setSelectedImage(result.assets[0]);
    }
  };

  const handleCaptureCamera = async () => {
    const { status } = await ImagePicker.requestCameraPermissionsAsync();
    if (status !== 'granted') {
      Alert.alert('Permission Denied', 'Camera permissions are required.');
      return;
    }

    const result = await ImagePicker.launchCameraAsync({
      allowsEditing: true,
      aspect: [1, 1],
      quality: 0.8,
    });

    if (!result.canceled && result.assets.length > 0) {
      setSelectedImage(result.assets[0]);
    }
  };

  const handleUploadImage = async () => {
    const code = barcode.trim();
    if (!code) {
      Alert.alert('Error', 'Please specify a barcode first.');
      return;
    }

    if (!selectedImage) {
      Alert.alert('Error', 'Please capture or select an image to upload.');
      return;
    }

    setUploading(true);

    try {
      const client = await getApiClient();
      const formData = new FormData();
      formData.append('barcodeNo', code);

      const filename = selectedImage.uri.split('/').pop() || 'photo.jpg';
      const match = /\.(\w+)$/.exec(filename);
      const type = match ? `image/${match[1]}` : 'image/jpeg';

      formData.append('image', {
        uri: selectedImage.uri,
        name: filename,
        type,
      } as any);

      const response = await client.post('/api/Barcode/upload', formData, {
        headers: { 'Content-Type': 'multipart/form-data' },
      });

      Alert.alert('Success', 'Barcode product photo uploaded successfully!');
      handleFetchBarcode(code);
      setSelectedImage(null);
    } catch (error: any) {
      const msg = error.response?.data?.message || 'Failed to upload photo.';
      Alert.alert('Upload Error', msg);
    } finally {
      setUploading(false);
    }
  };

  const handleSaveAdvanced = async () => {
    const code = barcode.trim();
    if (!code) {
      Alert.alert('Error', 'Please specify a barcode first.');
      return;
    }

    setSavingAdvanced(true);

    try {
      const client = await getApiClient();
      const payload: any = {
        barcode: code,
        size: editSize.trim() || undefined,
        reprintSticker,
      };

      if (editMrp.trim()) {
        payload.mrp = parseFloat(editMrp.trim());
      }

      await client.post('/api/Barcode/update-advanced', payload);
      Alert.alert('Success', 'Barcode details updated & sticker queued!');
      setReprintSticker(false);
      handleFetchBarcode(code);
    } catch (error: any) {
      const msg = error.response?.data?.message || 'Failed to update barcode.';
      Alert.alert('Update Error', msg);
    } finally {
      setSavingAdvanced(false);
    }
  };

  const currentImageUrl = product?.imageUrl
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
          <Text style={[styles.title, { color: theme.textPrimary }]}>
            Barcode Image & Sticker
          </Text>
          <Text style={[styles.subtitle, { color: theme.textMuted }]}>
            Capture jewellery tag photos, edit MRP/Size & reprint stickers
          </Text>
        </View>
      </View>

      {/* Barcode Input Card */}
      <GlassCard style={styles.card}>
        <View style={styles.inputRow}>
          <View style={{ flex: 1 }}>
            <GlassInput
              label="Article Barcode"
              value={barcode}
              onChangeText={setBarcode}
              placeholder="e.g. BC1002 or 000001"
              autoCapitalize="characters"
              onSubmitEditing={() => handleFetchBarcode(barcode)}
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
            onPress={() => handleFetchBarcode(barcode)}
            style={[
              styles.iconBtn,
              { backgroundColor: theme.cardBackground, borderColor: theme.cardBorder },
            ]}
          >
            {fetching ? (
              <ActivityIndicator size="small" color={theme.accent} />
            ) : (
              <Text style={{ fontSize: 18 }}>🔍</Text>
            )}
          </TouchableOpacity>
        </View>
      </GlassCard>

      {/* Product Summary Card */}
      {product && (
        <GlassCard style={styles.card} glow>
          <View style={styles.productSummaryRow}>
            {currentImageUrl ? (
              <Image
                source={{ uri: currentImageUrl }}
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
                <Text style={{ fontSize: 28 }}>📸</Text>
                <Text style={[styles.noImgText, { color: theme.textMuted }]}>No Photo</Text>
              </View>
            )}

            <View style={{ flex: 1, marginLeft: 14 }}>
              <Text style={[styles.productName, { color: theme.textPrimary }]} numberOfLines={2}>
                {product.itemName}
              </Text>
              <Text style={[styles.productPurity, { color: theme.accent }]}>
                {product.purity} • Net Wt: {product.netWt}g
              </Text>
              <Text style={[styles.productMrp, { color: theme.textSecondary }]}>
                MRP: ₹{product.mrp ? product.mrp.toLocaleString() : '0'} • Size: {product.size || '-'}
              </Text>
            </View>
          </View>
        </GlassCard>
      )}

      {/* Mode Switch Tabs */}
      <View style={styles.tabContainer}>
        <TouchableOpacity
          activeOpacity={0.8}
          onPress={() => setActiveTab('upload')}
          style={[
            styles.modeTab,
            {
              backgroundColor: activeTab === 'upload' ? theme.accent : theme.inputBackground,
              borderColor: activeTab === 'upload' ? theme.accent : theme.cardBorder,
            },
          ]}
        >
          <Text
            style={[
              styles.modeTabText,
              { color: activeTab === 'upload' ? theme.accentText : theme.textPrimary },
            ]}
          >
            📸 Photo Upload
          </Text>
        </TouchableOpacity>

        <TouchableOpacity
          activeOpacity={0.8}
          onPress={() => setActiveTab('advanced')}
          style={[
            styles.modeTab,
            {
              backgroundColor: activeTab === 'advanced' ? theme.accent : theme.inputBackground,
              borderColor: activeTab === 'advanced' ? theme.accent : theme.cardBorder,
            },
          ]}
        >
          <Text
            style={[
              styles.modeTabText,
              { color: activeTab === 'advanced' ? theme.accentText : theme.textPrimary },
            ]}
          >
            ⚡ Advanced / Sticker
          </Text>
        </TouchableOpacity>
      </View>

      {/* TAB 1: Photo Upload Section */}
      {activeTab === 'upload' && (
        <GlassCard style={styles.card}>
          <Text style={[styles.sectionTitle, { color: theme.textPrimary }]}>
            Capture & Upload Article Photo
          </Text>
          <Text style={[styles.sectionDesc, { color: theme.textMuted }]}>
            Take a showroom photo or choose an existing picture for this jewellery tag
          </Text>

          {/* Action Buttons */}
          <View style={styles.btnRow}>
            <TouchableOpacity
              activeOpacity={0.8}
              onPress={handleCaptureCamera}
              style={[
                styles.actionBtn,
                { backgroundColor: theme.inputBackground, borderColor: theme.cardBorder },
              ]}
            >
              <Text style={{ fontSize: 24, marginBottom: 4 }}>📷</Text>
              <Text style={[styles.actionBtnText, { color: theme.textPrimary }]}>Camera</Text>
            </TouchableOpacity>

            <TouchableOpacity
              activeOpacity={0.8}
              onPress={handlePickFromGallery}
              style={[
                styles.actionBtn,
                { backgroundColor: theme.inputBackground, borderColor: theme.cardBorder },
              ]}
            >
              <Text style={{ fontSize: 24, marginBottom: 4 }}>🖼️</Text>
              <Text style={[styles.actionBtnText, { color: theme.textPrimary }]}>Gallery</Text>
            </TouchableOpacity>
          </View>

          {/* Selected Preview */}
          {selectedImage && (
            <View style={styles.previewContainer}>
              <Text style={[styles.previewLabel, { color: theme.accent }]}>
                Selected New Photo:
              </Text>
              <Image
                source={{ uri: selectedImage.uri }}
                style={[styles.fullPreview, { borderColor: theme.accent }]}
                resizeMode="cover"
              />
              <TouchableOpacity
                onPress={() => setSelectedImage(null)}
                style={styles.removeBtn}
              >
                <Text style={styles.removeBtnText}>✕ Remove</Text>
              </TouchableOpacity>
            </View>
          )}

          <GlassButton
            title="Upload Photo to Server"
            onPress={handleUploadImage}
            loading={uploading}
            style={{ marginTop: 14 }}
          />
        </GlassCard>
      )}

      {/* TAB 2: Advanced / Sticker Reprint Section */}
      {activeTab === 'advanced' && (
        <GlassCard style={styles.card}>
          <Text style={[styles.sectionTitle, { color: theme.textPrimary }]}>
            Update MRP & Sticker Management
          </Text>
          <Text style={[styles.sectionDesc, { color: theme.textMuted }]}>
            Change selling MRP, adjust ring/bangle size, or queue sticker reprint
          </Text>

          <GlassInput
            label="Selling MRP (₹)"
            value={editMrp}
            onChangeText={setEditMrp}
            placeholder="e.g. 95000"
            keyboardType="numeric"
          />

          <GlassInput
            label="Article Size"
            value={editSize}
            onChangeText={setEditSize}
            placeholder="e.g. 16 Inch, 2.4, 14"
          />

          {/* Reprint Sticker Toggle */}
          <TouchableOpacity
            activeOpacity={0.8}
            onPress={() => setReprintSticker(!reprintSticker)}
            style={[
              styles.reprintToggle,
              {
                backgroundColor: reprintSticker ? theme.accent : theme.inputBackground,
                borderColor: reprintSticker ? theme.accent : theme.cardBorder,
              },
            ]}
          >
            <Text style={{ fontSize: 20 }}>🏷️</Text>
            <View style={{ flex: 1, marginLeft: 10 }}>
              <Text
                style={[
                  styles.reprintTitle,
                  { color: reprintSticker ? theme.accentText : theme.textPrimary },
                ]}
              >
                Reprint Barcode Sticker
              </Text>
              <Text
                style={[
                  styles.reprintDesc,
                  { color: reprintSticker ? theme.accentText : theme.textMuted },
                ]}
              >
                Flags barcode in ERP print queue for immediate sticker reprint
              </Text>
            </View>
            <Text style={{ fontSize: 18 }}>{reprintSticker ? '✅' : '⬜'}</Text>
          </TouchableOpacity>

          <GlassButton
            title="Save Changes & Queue Sticker"
            onPress={handleSaveAdvanced}
            loading={savingAdvanced}
            style={{ marginTop: 16 }}
          />
        </GlassCard>
      )}

      <BarcodeScannerModal
        visible={scannerVisible}
        onClose={() => setScannerVisible(false)}
        onScan={(scanned) => {
          setBarcode(scanned);
          handleFetchBarcode(scanned);
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
  productSummaryRow: { flexDirection: 'row', alignItems: 'center' },
  productThumb: { width: 68, height: 68, borderRadius: 12, borderWidth: 1 },
  productPlaceholder: {
    width: 68,
    height: 68,
    borderRadius: 12,
    borderWidth: 1,
    alignItems: 'center',
    justifyContent: 'center',
  },
  noImgText: { fontSize: 9, fontWeight: '700', marginTop: 2 },
  productName: { fontSize: 16, fontWeight: '900' },
  productPurity: { fontSize: 12, fontWeight: '700', marginTop: 2 },
  productMrp: { fontSize: 11, fontWeight: '600', marginTop: 2 },
  tabContainer: { flexDirection: 'row', gap: 10, marginBottom: 16 },
  modeTab: {
    flex: 1,
    paddingVertical: 12,
    borderRadius: 12,
    borderWidth: 1,
    alignItems: 'center',
  },
  modeTabText: { fontSize: 12, fontWeight: '800' },
  sectionTitle: { fontSize: 14, fontWeight: '800', marginBottom: 4 },
  sectionDesc: { fontSize: 11, marginBottom: 14, lineHeight: 16 },
  btnRow: { flexDirection: 'row', gap: 12, marginBottom: 14 },
  actionBtn: {
    flex: 1,
    paddingVertical: 14,
    borderRadius: 12,
    borderWidth: 1,
    alignItems: 'center',
  },
  actionBtnText: { fontSize: 13, fontWeight: '800' },
  previewContainer: { alignItems: 'center', marginVertical: 10 },
  previewLabel: { fontSize: 11, fontWeight: '800', marginBottom: 6 },
  fullPreview: { width: 140, height: 140, borderRadius: 14, borderWidth: 2 },
  removeBtn: { marginTop: 6 },
  removeBtnText: { color: '#EF4444', fontSize: 12, fontWeight: '700' },
  reprintToggle: {
    flexDirection: 'row',
    alignItems: 'center',
    padding: 12,
    borderRadius: 12,
    borderWidth: 1,
    marginTop: 6,
  },
  reprintTitle: { fontSize: 13, fontWeight: '800' },
  reprintDesc: { fontSize: 10, marginTop: 2 },
});
