import React, { useState } from 'react';
import { View, Text, StyleSheet, ScrollView, Image, TouchableOpacity, Alert } from 'react-native';
import * as ImagePicker from 'expo-image-picker';
import { useTheme } from '../theme/ThemeContext';
import { getServerUrl } from '../config/serverConfig';
import { getStoredToken } from '../config/apiService';
import { GlassCard } from '../components/GlassCard';
import { GlassInput } from '../components/GlassInput';
import { GlassButton } from '../components/GlassButton';

export const DocUploadScreen: React.FC = () => {
  const { theme } = useTheme();

  const [imageUri, setImageUri] = useState<string | null>(null);
  const [docCategory, setDocCategory] = useState<string>('KYC / Aadhaar / PAN');
  const [notes, setNotes] = useState<string>('');
  const [uploading, setUploading] = useState<boolean>(false);

  const categories = ['KYC / Aadhaar / PAN', 'Custom Design Sketch', 'Invoice Copy', 'Customer ID'];

  const takePhoto = async () => {
    const permissionResult = await ImagePicker.requestCameraPermissionsAsync();
    if (!permissionResult.granted) {
      Alert.alert('Permission Denied', 'Camera access is required to take photo.');
      return;
    }

    const result = await ImagePicker.launchCameraAsync({
      mediaTypes: ImagePicker.MediaTypeOptions.Images,
      quality: 0.8,
    });

    if (!result.canceled && result.assets && result.assets.length > 0) {
      setImageUri(result.assets[0].uri);
    }
  };

  const pickImage = async () => {
    const result = await ImagePicker.launchImageLibraryAsync({
      mediaTypes: ImagePicker.MediaTypeOptions.Images,
      quality: 0.8,
    });

    if (!result.canceled && result.assets && result.assets.length > 0) {
      setImageUri(result.assets[0].uri);
    }
  };

  const handleUpload = async () => {
    if (!imageUri) {
      Alert.alert('Error', 'Please select or take a photo first.');
      return;
    }

    setUploading(true);
    try {
      const baseUrl = await getServerUrl();
      const token = await getStoredToken();

      const formData = new FormData();
      const filename = imageUri.split('/').pop() || 'upload.jpg';
      const match = /\.(\w+)$/.exec(filename);
      const type = match ? `image/${match[1]}` : 'image/jpeg';

      // @ts-ignore
      formData.append('file', { uri: imageUri, name: filename, type });
      formData.append('category', docCategory);
      formData.append('notes', notes);

      const response = await fetch(`${baseUrl}/api/UploadDocs`, {
        method: 'POST',
        headers: {
          ...(token ? { Authorization: `Bearer ${token}` } : {}),
        },
        body: formData,
      });

      if (response.ok) {
        Alert.alert('Success', 'Document uploaded successfully to server Docs folder!');
        setImageUri(null);
        setNotes('');
      } else {
        const errorText = await response.text();
        Alert.alert('Upload Failed', errorText || 'Server error while saving document.');
      }
    } catch (error: any) {
      Alert.alert('Upload Error', error.message || 'Network error while uploading.');
    } finally {
      setUploading(false);
    }
  };

  return (
    <ScrollView style={[styles.container, { backgroundColor: theme.background }]}>
      <View style={styles.content}>
        <Text style={[styles.title, { color: theme.textPrimary }]}>Document & KYC Scanner</Text>
        <Text style={[styles.subtitle, { color: theme.textMuted }]}>
          Capture customer IDs, custom design sketches, or invoices and save to ERP server
        </Text>

        <GlassCard style={styles.card}>
          <Text style={[styles.sectionTitle, { color: theme.accent }]}>1. Select Document Category</Text>
          <View style={styles.catGrid}>
            {categories.map((cat) => {
              const isSelected = docCategory === cat;
              return (
                <TouchableOpacity
                  key={cat}
                  activeOpacity={0.8}
                  onPress={() => setDocCategory(cat)}
                  style={[
                    styles.catChip,
                    {
                      backgroundColor: isSelected ? theme.accent : theme.inputBackground,
                      borderColor: isSelected ? theme.accent : theme.inputBorder,
                    },
                  ]}
                >
                  <Text
                    style={[
                      styles.catChipText,
                      { color: isSelected ? theme.accentText : theme.textPrimary },
                    ]}
                  >
                    {cat}
                  </Text>
                </TouchableOpacity>
              );
            })}
          </View>

          <Text style={[styles.sectionTitle, { color: theme.accent, marginTop: 14 }]}>
            2. Photo Capture / Gallery
          </Text>

          {imageUri ? (
            <View style={styles.previewContainer}>
              <Image source={{ uri: imageUri }} style={styles.previewImage} />
              <TouchableOpacity
                onPress={() => setImageUri(null)}
                style={[styles.removeBtn, { backgroundColor: theme.statusError }]}
              >
                <Text style={{ color: '#FFF', fontWeight: '800' }}>✕ Remove Photo</Text>
              </TouchableOpacity>
            </View>
          ) : (
            <View style={styles.btnRow}>
              <TouchableOpacity
                activeOpacity={0.8}
                onPress={takePhoto}
                style={[styles.actionCard, { backgroundColor: theme.inputBackground, borderColor: theme.inputBorder }]}
              >
                <Text style={{ fontSize: 32 }}>📷</Text>
                <Text style={[styles.actionText, { color: theme.textPrimary }]}>Take Photo</Text>
              </TouchableOpacity>

              <TouchableOpacity
                activeOpacity={0.8}
                onPress={pickImage}
                style={[styles.actionCard, { backgroundColor: theme.inputBackground, borderColor: theme.inputBorder }]}
              >
                <Text style={{ fontSize: 32 }}>🖼️</Text>
                <Text style={[styles.actionText, { color: theme.textPrimary }]}>Pick Gallery</Text>
              </TouchableOpacity>
            </View>
          )}

          <GlassInput
            label="Additional Notes / Remarks"
            value={notes}
            onChangeText={setNotes}
            placeholder="e.g. Customer Aadhaar for PAN billing approval"
            multiline
            numberOfLines={2}
          />

          <GlassButton
            title="Upload Document to ERP"
            onPress={handleUpload}
            loading={uploading}
            disabled={!imageUri}
            style={{ marginTop: 12 }}
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
  sectionTitle: { fontSize: 14, fontWeight: '700', marginBottom: 10 },
  catGrid: { flexDirection: 'row', flexWrap: 'wrap', gap: 8, marginBottom: 8 },
  catChip: { paddingHorizontal: 12, paddingVertical: 8, borderRadius: 10, borderWidth: 1 },
  catChipText: { fontSize: 12, fontWeight: '700' },
  btnRow: { flexDirection: 'row', gap: 12, marginBottom: 16 },
  actionCard: {
    flex: 1,
    height: 110,
    borderRadius: 14,
    borderWidth: 1,
    alignItems: 'center',
    justifyContent: 'center',
  },
  actionText: { fontSize: 13, fontWeight: '700', marginTop: 6 },
  previewContainer: { alignItems: 'center', marginBottom: 16 },
  previewImage: { width: '100%', height: 220, borderRadius: 14, marginBottom: 10 },
  removeBtn: { paddingHorizontal: 14, paddingVertical: 6, borderRadius: 8 },
});
