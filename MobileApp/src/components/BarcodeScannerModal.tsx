import React, { useState, useEffect } from 'react';
import { Modal, View, Text, StyleSheet, TouchableOpacity } from 'react-native';
import { CameraView, useCameraPermissions } from 'expo-camera';
import { useTheme } from '../theme/ThemeContext';
import { GlassButton } from './GlassButton';

interface BarcodeScannerModalProps {
  visible: boolean;
  onClose: () => void;
  onScan: (barcode: string) => void;
}

export const BarcodeScannerModal: React.FC<BarcodeScannerModalProps> = ({
  visible,
  onClose,
  onScan,
}) => {
  const { theme } = useTheme();
  const [permission, requestPermission] = useCameraPermissions();
  const [scanned, setScanned] = useState(false);

  useEffect(() => {
    if (visible) {
      setScanned(false);
      if (!permission?.granted) {
        requestPermission().catch(() => {});
      }
    }
  }, [visible]);

  const handleBarcodeScanned = (result: any) => {
    const data = result?.data || '';
    if (!scanned && data) {
      setScanned(true);
      onScan(data);
      onClose();
    }
  };

  if (!visible) return null;

  return (
    <Modal
      visible={visible}
      animationType="slide"
      transparent
      onRequestClose={onClose}
    >
      <View style={styles.overlay}>
        <View
          style={[
            styles.modalContainer,
            { backgroundColor: theme.background, borderColor: theme.cardBorder },
          ]}
        >
          <View style={styles.header}>
            <Text style={[styles.title, { color: theme.textPrimary }]}>
              📷 Scan Jewellery Tag
            </Text>
            <TouchableOpacity onPress={onClose} style={styles.closeBtn}>
              <Text style={{ fontSize: 20, color: theme.textMuted }}>✕</Text>
            </TouchableOpacity>
          </View>

          {!permission ? (
            <View style={{ padding: 24, alignItems: 'center' }}>
              <Text style={{ color: theme.textMuted, textAlign: 'center' }}>
                Checking camera permissions...
              </Text>
            </View>
          ) : !permission.granted ? (
            <View style={{ padding: 24, alignItems: 'center' }}>
              <Text
                style={{
                  color: theme.statusError,
                  textAlign: 'center',
                  marginBottom: 16,
                  lineHeight: 20,
                }}
              >
                Camera permission is needed to scan jewellery barcodes.
              </Text>
              <GlassButton
                title="Grant Permission"
                onPress={() => requestPermission()}
                style={{ marginBottom: 10, width: '100%' }}
              />
              <GlassButton
                title="Close"
                variant="secondary"
                onPress={onClose}
                style={{ width: '100%' }}
              />
            </View>
          ) : (
            <View style={styles.cameraFrame}>
              <CameraView
                facing="back"
                barcodeScannerSettings={{
                  barcodeTypes: [
                    'qr',
                    'code128',
                    'code39',
                    'code93',
                    'ean13',
                    'ean8',
                    'upc_a',
                    'upc_e',
                    'itf14',
                    'codabar',
                  ],
                }}
                onBarcodeScanned={scanned ? undefined : handleBarcodeScanned}
                style={StyleSheet.absoluteFillObject}
              />
              <View style={[styles.scanReticle, { borderColor: theme.accent }]} />
              <Text style={[styles.instruction, { color: '#FFFFFF' }]}>
                Position jewellery barcode inside the target box
              </Text>
            </View>
          )}
        </View>
      </View>
    </Modal>
  );
};

const styles = StyleSheet.create({
  overlay: {
    flex: 1,
    backgroundColor: 'rgba(0,0,0,0.75)',
    justifyContent: 'center',
    padding: 20,
  },
  modalContainer: {
    borderRadius: 20,
    borderWidth: 1,
    overflow: 'hidden',
  },
  header: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    padding: 16,
  },
  title: {
    fontSize: 16,
    fontWeight: '800',
  },
  closeBtn: {
    padding: 4,
  },
  cameraFrame: {
    height: 320,
    justifyContent: 'center',
    alignItems: 'center',
    overflow: 'hidden',
  },
  scanReticle: {
    width: 220,
    height: 140,
    borderWidth: 2,
    borderRadius: 12,
    backgroundColor: 'rgba(255,255,255,0.05)',
  },
  instruction: {
    position: 'absolute',
    bottom: 20,
    fontSize: 12,
    fontWeight: '700',
    backgroundColor: 'rgba(0,0,0,0.6)',
    paddingHorizontal: 12,
    paddingVertical: 6,
    borderRadius: 8,
  },
});
