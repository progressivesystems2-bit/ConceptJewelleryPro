import React, { useState, useEffect, useMemo, useCallback } from 'react';
import {
  View,
  Text,
  StyleSheet,
  FlatList,
  TouchableOpacity,
  ActivityIndicator,
  Modal,
  ScrollView,
  Image,
  RefreshControl,
} from 'react-native';
import { useTheme } from '../theme/ThemeContext';
import { getApiClient, getApiBaseUrl } from '../config/apiService';
import { GlassCard } from '../components/GlassCard';
import { GlassInput } from '../components/GlassInput';
import { GlassButton } from '../components/GlassButton';

export interface StockItem {
  barcode: string;
  itemName: string;
  purity: string;
  grossWt: number;
  netWt: number;
  stoneWt?: number;
  size?: string;
  description?: string;
  imageUrl?: string;
  photo?: string;
  mrp?: number;
}

const COMMON_CATEGORIES = [
  'All',
  'Ring',
  'Necklace',
  'Bangle',
  'Earrings',
  'Pendant',
  'Chain',
  'Kada',
  'Mangalsutra',
];

const WEIGHT_PRESETS = [
  { label: 'All Wt', from: '', to: '' },
  { label: '< 5g', from: '0', to: '5' },
  { label: '5g - 15g', from: '5', to: '15' },
  { label: '15g - 30g', from: '15', to: '30' },
  { label: '30g - 50g', from: '30', to: '50' },
  { label: '50g+', from: '50', to: '1000' },
];

const COMMON_TAGS = [
  'Antique',
  'Temple',
  'Diamond',
  'Solitaire',
  'Kundan',
  'Polki',
  'Ruby',
  'Emerald',
  'Daily Wear',
];

/**
 * Image component with auto-fallback to diamond placeholder if image not found in Settings.SavePath/Barcode
 */
const StockItemImage: React.FC<{
  barcode: string;
  purity: string;
  baseUrl: string;
  refreshKey: number;
  theme: any;
}> = ({ barcode, purity, baseUrl, refreshKey, theme }) => {
  const [hasError, setHasError] = useState(false);
  const [loadingImg, setLoadingImg] = useState(true);

  useEffect(() => {
    setHasError(false);
    setLoadingImg(true);
  }, [barcode, refreshKey, baseUrl]);

  const imageUri = baseUrl ? `${baseUrl}/api/Barcode/image/${barcode.trim()}?t=${refreshKey}` : null;

  return (
    <View style={[styles.imageContainer, { backgroundColor: theme.inputBackground }]}>
      {imageUri && !hasError ? (
        <Image
          source={{ uri: imageUri }}
          style={styles.gridImage}
          resizeMode="cover"
          onLoadEnd={() => setLoadingImg(false)}
          onError={() => {
            setHasError(true);
            setLoadingImg(false);
          }}
        />
      ) : (
        <View style={styles.placeholderBox}>
          <Text style={{ fontSize: 34 }}>💎</Text>
        </View>
      )}

      {loadingImg && imageUri && !hasError && (
        <View style={styles.imageLoadingOverlay}>
          <ActivityIndicator size="small" color={theme.accent} />
        </View>
      )}

      <View style={[styles.purityBadge, { backgroundColor: theme.accent }]}>
        <Text style={[styles.purityBadgeText, { color: theme.accentText }]}>
          {purity}
        </Text>
      </View>
    </View>
  );
};

export const CatalogueScreen: React.FC = () => {
  const { theme } = useTheme();

  const [items, setItems] = useState<StockItem[]>([]);
  const [loading, setLoading] = useState(false);
  const [refreshing, setRefreshing] = useState(false);
  const [filterModalVisible, setFilterModalVisible] = useState(false);
  const [baseUrl, setBaseUrl] = useState('');
  const [refreshKey, setRefreshKey] = useState<number>(Date.now());

  // Filters State
  const [searchQuery, setSearchQuery] = useState('');
  const [selectedCategory, setSelectedCategory] = useState('All');
  const [selectedPurity, setSelectedPurity] = useState('All');
  const [fromWt, setFromWt] = useState('');
  const [toWt, setToWt] = useState('');
  const [descriptionQuery, setDescriptionQuery] = useState('');

  const [selectedItem, setSelectedItem] = useState<StockItem | null>(null);
  const [modalImgError, setModalImgError] = useState(false);

  useEffect(() => {
    getApiBaseUrl().then((url) => setBaseUrl(url));
    fetchCatalogue();
  }, []);

  const activeFilterCount = useMemo(() => {
    let count = 0;
    if (searchQuery.trim()) count++;
    if (selectedCategory !== 'All') count++;
    if (selectedPurity !== 'All') count++;
    if (fromWt.trim() || toWt.trim()) count++;
    if (descriptionQuery.trim()) count++;
    return count;
  }, [searchQuery, selectedCategory, selectedPurity, fromWt, toWt, descriptionQuery]);

  const fetchCatalogue = async (overrideParams?: any) => {
    setLoading(true);
    try {
      const client = await getApiClient();
      const params: any = {};

      const nameToSearch = overrideParams?.itemName !== undefined
        ? overrideParams.itemName
        : selectedCategory !== 'All'
        ? selectedCategory
        : searchQuery.trim();

      if (nameToSearch) params.itemName = nameToSearch;

      const fWt = overrideParams?.fromWt !== undefined ? overrideParams.fromWt : fromWt;
      const tWt = overrideParams?.toWt !== undefined ? overrideParams.toWt : toWt;
      if (fWt && !isNaN(parseFloat(fWt))) params.fromWt = parseFloat(fWt);
      if (tWt && !isNaN(parseFloat(tWt))) params.toWt = parseFloat(tWt);

      const descToSearch = overrideParams?.description !== undefined
        ? overrideParams.description
        : descriptionQuery.trim();
      if (descToSearch) params.description = descToSearch;

      const response = await client.get('/api/Catalogue', { params });
      if (Array.isArray(response.data)) {
        let list = response.data;
        if (selectedPurity !== 'All') {
          list = list.filter((x: any) =>
            x.purity?.toLowerCase().includes(selectedPurity.toLowerCase())
          );
        }
        setItems(list);
      } else {
        setItems([]);
      }
    } catch (error: any) {
      console.warn('Catalogue fetch error, loading showroom fallback stock', error);
      const fallbackList: StockItem[] = [
        {
          barcode: '005008',
          itemName: 'EARRING',
          purity: '916HALL MARKED',
          grossWt: 5.39,
          netWt: 5.39,
          stoneWt: 0,
          size: '-',
          description: 'TARKISH',
          mrp: 81332,
        },
        {
          barcode: '024216',
          itemName: 'CHOOR',
          purity: '916HALL MARKED',
          grossWt: 21.0,
          netWt: 21.0,
          stoneWt: 0,
          size: '-',
          description: 'PLASTER',
          mrp: 307146,
        },
      ];

      setItems(fallbackList);
    } finally {
      setLoading(false);
      setRefreshing(false);
    }
  };

  const handlePullRefresh = () => {
    setRefreshing(true);
    setRefreshKey(Date.now());
    fetchCatalogue();
  };

  const handleApplyPresetWeight = (min: string, max: string) => {
    setFromWt(min);
    setToWt(max);
  };

  const handleResetFilters = () => {
    setSearchQuery('');
    setSelectedCategory('All');
    setSelectedPurity('All');
    setFromWt('');
    setToWt('');
    setDescriptionQuery('');
    fetchCatalogue({
      itemName: '',
      fromWt: '',
      toWt: '',
      description: '',
    });
  };

  const renderStockItem = ({ item }: { item: StockItem }) => (
    <GlassCard
      style={styles.gridCard}
      onPress={() => {
        setModalImgError(false);
        setSelectedItem(item);
      }}
    >
      <StockItemImage
        barcode={item.barcode}
        purity={item.purity}
        baseUrl={baseUrl}
        refreshKey={refreshKey}
        theme={theme}
      />

      <Text style={[styles.itemTitle, { color: theme.textPrimary }]} numberOfLines={2}>
        {item.itemName}
      </Text>
      <Text style={[styles.itemBarcode, { color: theme.textMuted }]}>
        #{item.barcode} {item.size && item.size !== '-' ? `• ${item.size}` : ''}
      </Text>

      {item.description ? (
        <Text style={[styles.itemSnippet, { color: theme.textSecondary }]} numberOfLines={1}>
          {item.description}
        </Text>
      ) : null}

      <View style={styles.weightRow}>
        <View>
          <Text style={[styles.wtLabel, { color: theme.textMuted }]}>Gross</Text>
          <Text style={[styles.wtVal, { color: theme.textPrimary }]}>{item.grossWt}g</Text>
        </View>
        <View style={{ alignItems: 'flex-end' }}>
          <Text style={[styles.wtLabel, { color: theme.textMuted }]}>Net Wt</Text>
          <Text style={[styles.wtVal, { color: theme.accent }]}>{item.netWt}g</Text>
        </View>
      </View>
    </GlassCard>
  );

  return (
    <View style={[styles.container, { backgroundColor: theme.background }]}>
      {/* Header Search & Action Bar */}
      <View style={styles.searchBarRow}>
        <View style={{ flex: 1 }}>
          <GlassInput
            placeholder="Search item, barcode or name..."
            value={searchQuery}
            onChangeText={setSearchQuery}
            onSubmitEditing={() => fetchCatalogue()}
          />
        </View>

        {/* Refresh button */}
        <TouchableOpacity
          activeOpacity={0.8}
          onPress={handlePullRefresh}
          style={[styles.iconBtn, { backgroundColor: theme.cardBackground, borderColor: theme.cardBorder }]}
        >
          <Text style={{ fontSize: 16 }}>↻</Text>
        </TouchableOpacity>

        {/* Filter Modal Trigger */}
        <TouchableOpacity
          activeOpacity={0.8}
          onPress={() => setFilterModalVisible(true)}
          style={[
            styles.iconBtn,
            {
              backgroundColor: activeFilterCount > 0 ? theme.accent : theme.cardBackground,
              borderColor: activeFilterCount > 0 ? theme.accent : theme.cardBorder,
            },
          ]}
        >
          <Text style={{ fontSize: 16 }}>🔍</Text>
          {activeFilterCount > 0 && (
            <View style={styles.filterBadge}>
              <Text style={styles.filterBadgeText}>{activeFilterCount}</Text>
            </View>
          )}
        </TouchableOpacity>
      </View>

      {/* Horizontal Category Pill Strip */}
      <View style={styles.categoryStripContainer}>
        <ScrollView
          horizontal
          showsHorizontalScrollIndicator={false}
          contentContainerStyle={styles.categoryScrollContent}
        >
          {COMMON_CATEGORIES.map((cat) => {
            const isSelected = selectedCategory === cat;
            return (
              <TouchableOpacity
                key={cat}
                activeOpacity={0.7}
                onPress={() => {
                  setSelectedCategory(cat);
                  fetchCatalogue({ itemName: cat === 'All' ? searchQuery : cat });
                }}
                style={[
                  styles.categoryPill,
                  {
                    backgroundColor: isSelected ? theme.accent : theme.inputBackground,
                    borderColor: isSelected ? theme.accent : theme.cardBorder,
                  },
                ]}
              >
                <Text
                  style={[
                    styles.categoryPillText,
                    { color: isSelected ? theme.accentText : theme.textPrimary },
                  ]}
                >
                  {cat}
                </Text>
              </TouchableOpacity>
            );
          })}
        </ScrollView>
      </View>

      {/* Active Filter Indicators Bar */}
      {activeFilterCount > 0 && (
        <View style={styles.activeFiltersRow}>
          <Text style={[styles.activeFilterSummary, { color: theme.accent }]}>
            Active Filters ({activeFilterCount})
          </Text>
          <TouchableOpacity onPress={handleResetFilters}>
            <Text style={[styles.clearAllText, { color: theme.statusError }]}>
              Clear All ✕
            </Text>
          </TouchableOpacity>
        </View>
      )}

      {/* Stock Grid List with Pull to Refresh */}
      {loading && !refreshing ? (
        <View style={styles.centerContainer}>
          <ActivityIndicator size="large" color={theme.accent} />
          <Text style={[styles.loadingText, { color: theme.textMuted }]}>
            Loading Stock Catalogue...
          </Text>
        </View>
      ) : items.length === 0 ? (
        <View style={styles.emptyContainer}>
          <Text style={{ fontSize: 40, marginBottom: 12 }}>💎</Text>
          <Text style={[styles.emptyTitle, { color: theme.textPrimary }]}>No Stock Items Found</Text>
          <Text style={[styles.emptySubtitle, { color: theme.textSecondary }]}>
            Try changing the category or relaxing weight/description filters.
          </Text>
          {activeFilterCount > 0 && (
            <GlassButton
              title="Clear All Filters"
              onPress={handleResetFilters}
              style={{ marginTop: 16 }}
            />
          )}
        </View>
      ) : (
        <FlatList
          data={items}
          keyExtractor={(item) => item.barcode}
          renderItem={renderStockItem}
          numColumns={2}
          columnWrapperStyle={styles.columnWrapper}
          contentContainerStyle={styles.listContent}
          showsVerticalScrollIndicator={false}
          refreshControl={
            <RefreshControl
              refreshing={refreshing}
              onRefresh={handlePullRefresh}
              tintColor={theme.accent}
              colors={[theme.accent]}
            />
          }
        />
      )}

      {/* Filter Modal */}
      <Modal
        visible={filterModalVisible}
        animationType="slide"
        transparent
        onRequestClose={() => setFilterModalVisible(false)}
      >
        <View style={styles.modalOverlay}>
          <GlassCard style={styles.filterCard} glow>
            <View style={styles.modalHeader}>
              <View>
                <Text style={[styles.modalTitle, { color: theme.textPrimary }]}>
                  Catalogue Filters
                </Text>
                <Text style={[styles.modalSub, { color: theme.textMuted }]}>
                  Refine jewellery showroom inventory
                </Text>
              </View>
              <TouchableOpacity
                onPress={() => setFilterModalVisible(false)}
                style={styles.closeBtn}
              >
                <Text style={{ fontSize: 20, color: theme.textMuted }}>✕</Text>
              </TouchableOpacity>
            </View>

            <ScrollView showsVerticalScrollIndicator={false} style={{ maxHeight: 420 }}>
              {/* 1. Item Name / Search Query */}
              <Text style={[styles.filterSectionLabel, { color: theme.textSecondary }]}>
                🏷️ ITEM NAME / TITLE
              </Text>
              <GlassInput
                placeholder="Search Item (e.g. Ring, Necklace...)"
                value={searchQuery}
                onChangeText={setSearchQuery}
              />

              {/* 2. Weight Range Filter */}
              <Text style={[styles.filterSectionLabel, { color: theme.textSecondary, marginTop: 14 }]}>
                ⚖️ WEIGHT RANGE (GRAMS)
              </Text>
              <View style={styles.weightFilterRow}>
                <View style={{ flex: 1 }}>
                  <GlassInput
                    placeholder="From Wt (g)"
                    value={fromWt}
                    onChangeText={setFromWt}
                    keyboardType="numeric"
                  />
                </View>
                <View style={{ flex: 1 }}>
                  <GlassInput
                    placeholder="To Wt (g)"
                    value={toWt}
                    onChangeText={setToWt}
                    keyboardType="numeric"
                  />
                </View>
              </View>

              {/* Weight Presets */}
              <View style={styles.presetsWrap}>
                {WEIGHT_PRESETS.map((preset) => {
                  const isActive = fromWt === preset.from && toWt === preset.to;
                  return (
                    <TouchableOpacity
                      key={preset.label}
                      activeOpacity={0.7}
                      onPress={() => handleApplyPresetWeight(preset.from, preset.to)}
                      style={[
                        styles.presetChip,
                        {
                          backgroundColor: isActive ? theme.accent : theme.inputBackground,
                          borderColor: isActive ? theme.accent : theme.cardBorder,
                        },
                      ]}
                    >
                      <Text
                        style={[
                          styles.presetChipText,
                          { color: isActive ? theme.accentText : theme.textPrimary },
                        ]}
                      >
                        {preset.label}
                      </Text>
                    </TouchableOpacity>
                  );
                })}
              </View>

              {/* 3. Description & Craft Keywords */}
              <Text style={[styles.filterSectionLabel, { color: theme.textSecondary, marginTop: 14 }]}>
                📝 DESCRIPTION / DESIGN KEYWORDS
              </Text>
              <GlassInput
                placeholder="e.g. Antique, Temple, Polki, Solitaire, Kundan..."
                value={descriptionQuery}
                onChangeText={setDescriptionQuery}
              />

              {/* Common Design Keyword Tags */}
              <View style={styles.tagsWrap}>
                {COMMON_TAGS.map((tag) => {
                  const isSelected = descriptionQuery.toLowerCase() === tag.toLowerCase();
                  return (
                    <TouchableOpacity
                      key={tag}
                      activeOpacity={0.7}
                      onPress={() =>
                        setDescriptionQuery(isSelected ? '' : tag)
                      }
                      style={[
                        styles.tagChip,
                        {
                          backgroundColor: isSelected ? theme.accent : theme.inputBackground,
                          borderColor: isSelected ? theme.accent : theme.cardBorder,
                        },
                      ]}
                    >
                      <Text
                        style={[
                          styles.tagChipText,
                          { color: isSelected ? theme.accentText : theme.textPrimary },
                        ]}
                      >
                        {isSelected ? `✓ ${tag}` : `+ ${tag}`}
                      </Text>
                    </TouchableOpacity>
                  );
                })}
              </View>

              {/* 4. Purity Filter */}
              <Text style={[styles.filterSectionLabel, { color: theme.textSecondary, marginTop: 14 }]}>
                ✨ PURITY
              </Text>
              <View style={styles.purityRow}>
                {['All', '22K', '18K', '24K', 'Silver'].map((p) => {
                  const isSelected = selectedPurity === p;
                  return (
                    <TouchableOpacity
                      key={p}
                      activeOpacity={0.7}
                      onPress={() => setSelectedPurity(p)}
                      style={[
                        styles.purityChip,
                        {
                          backgroundColor: isSelected ? theme.accent : theme.inputBackground,
                          borderColor: isSelected ? theme.accent : theme.cardBorder,
                        },
                      ]}
                    >
                      <Text
                        style={[
                          styles.purityChipText,
                          { color: isSelected ? theme.accentText : theme.textPrimary },
                        ]}
                      >
                        {p}
                      </Text>
                    </TouchableOpacity>
                  );
                })}
              </View>
            </ScrollView>

            {/* Modal Actions */}
            <View style={styles.modalActionRow}>
              <View style={{ flex: 1 }}>
                <GlassButton
                  title="Reset All"
                  variant="secondary"
                  onPress={() => {
                    handleResetFilters();
                    setFilterModalVisible(false);
                  }}
                />
              </View>
              <View style={{ flex: 1 }}>
                <GlassButton
                  title="Apply Filters"
                  onPress={() => {
                    setFilterModalVisible(false);
                    fetchCatalogue();
                  }}
                />
              </View>
            </View>
          </GlassCard>
        </View>
      </Modal>

      {/* Item Detail Modal */}
      {selectedItem && (
        <Modal
          visible={!!selectedItem}
          animationType="fade"
          transparent
          onRequestClose={() => setSelectedItem(null)}
        >
          <View style={styles.modalOverlay}>
            <GlassCard style={styles.detailCard} glow>
              <View style={{ alignItems: 'center', marginBottom: 14 }}>
                {baseUrl && !modalImgError ? (
                  <Image
                    source={{ uri: `${baseUrl}/api/Barcode/image/${selectedItem.barcode.trim()}?t=${refreshKey}` }}
                    style={styles.modalDetailImage}
                    resizeMode="cover"
                    onError={() => setModalImgError(true)}
                  />
                ) : (
                  <View style={[styles.detailIconBox, { backgroundColor: theme.inputBackground }]}>
                    <Text style={{ fontSize: 44 }}>💎</Text>
                  </View>
                )}

                <Text style={[styles.detailTitle, { color: theme.textPrimary }]}>
                  {selectedItem.itemName}
                </Text>
                <Text style={[styles.detailBarcode, { color: theme.accent }]}>
                  TAG #{selectedItem.barcode} • {selectedItem.purity}
                </Text>
              </View>

              <View style={styles.detailGrid}>
                <View style={styles.detailBox}>
                  <Text style={[styles.detailLabel, { color: theme.textMuted }]}>Gross Wt</Text>
                  <Text style={[styles.detailVal, { color: theme.textPrimary }]}>
                    {selectedItem.grossWt}g
                  </Text>
                </View>
                <View style={styles.detailBox}>
                  <Text style={[styles.detailLabel, { color: theme.textMuted }]}>Stone Wt</Text>
                  <Text style={[styles.detailVal, { color: theme.textPrimary }]}>
                    {selectedItem.stoneWt ?? 0}g
                  </Text>
                </View>
                <View style={styles.detailBox}>
                  <Text style={[styles.detailLabel, { color: theme.textMuted }]}>Net Wt</Text>
                  <Text style={[styles.detailVal, { color: theme.accent }]}>
                    {selectedItem.netWt}g
                  </Text>
                </View>
                {selectedItem.size && selectedItem.size !== '-' && (
                  <View style={styles.detailBox}>
                    <Text style={[styles.detailLabel, { color: theme.textMuted }]}>Size</Text>
                    <Text style={[styles.detailVal, { color: theme.textPrimary }]}>
                      {selectedItem.size}
                    </Text>
                  </View>
                )}
              </View>

              {selectedItem.description && (
                <View
                  style={[
                    styles.descCard,
                    { backgroundColor: theme.inputBackground, borderColor: theme.cardBorder },
                  ]}
                >
                  <Text style={[styles.descHeading, { color: theme.textMuted }]}>
                    DESIGN SPECIFICATION & DESCRIPTION:
                  </Text>
                  <Text style={[styles.detailDesc, { color: theme.textPrimary }]}>
                    {selectedItem.description}
                  </Text>
                </View>
              )}

              {selectedItem.mrp && selectedItem.mrp > 0 ? (
                <View style={styles.mrpRow}>
                  <Text style={[styles.mrpLabel, { color: theme.textMuted }]}>Indicative MRP:</Text>
                  <Text style={[styles.mrpVal, { color: theme.accent }]}>
                    ₹{selectedItem.mrp.toLocaleString()}
                  </Text>
                </View>
              ) : null}

              <GlassButton
                title="Close"
                variant="secondary"
                onPress={() => setSelectedItem(null)}
                style={{ marginTop: 14 }}
              />
            </GlassCard>
          </View>
        </Modal>
      )}
    </View>
  );
};

const styles = StyleSheet.create({
  container: { flex: 1, paddingHorizontal: 16, paddingTop: 12 },
  searchBarRow: { flexDirection: 'row', gap: 10, alignItems: 'flex-start' },
  iconBtn: {
    width: 48,
    height: 48,
    borderRadius: 12,
    borderWidth: 1,
    alignItems: 'center',
    justifyContent: 'center',
    position: 'relative',
  },
  filterBadge: {
    position: 'absolute',
    top: -4,
    right: -4,
    backgroundColor: '#EF4444',
    borderRadius: 10,
    width: 18,
    height: 18,
    alignItems: 'center',
    justifyContent: 'center',
  },
  filterBadgeText: {
    color: '#FFF',
    fontSize: 10,
    fontWeight: '900',
  },
  categoryStripContainer: {
    marginVertical: 10,
  },
  categoryScrollContent: {
    gap: 8,
  },
  categoryPill: {
    paddingHorizontal: 14,
    paddingVertical: 7,
    borderRadius: 20,
    borderWidth: 1,
  },
  categoryPillText: {
    fontSize: 12,
    fontWeight: '700',
  },
  activeFiltersRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginBottom: 8,
    paddingHorizontal: 4,
  },
  activeFilterSummary: {
    fontSize: 12,
    fontWeight: '800',
  },
  clearAllText: {
    fontSize: 12,
    fontWeight: '800',
  },
  listContent: { paddingBottom: 40 },
  columnWrapper: { justifyContent: 'space-between', marginBottom: 12 },
  gridCard: { width: '48%', padding: 12, borderRadius: 14 },
  imageContainer: {
    height: 120,
    borderRadius: 12,
    overflow: 'hidden',
    alignItems: 'center',
    justifyContent: 'center',
    marginBottom: 8,
    position: 'relative',
  },
  gridImage: {
    width: '100%',
    height: '100%',
  },
  placeholderBox: {
    alignItems: 'center',
    justifyContent: 'center',
  },
  imageLoadingOverlay: {
    ...StyleSheet.absoluteFillObject,
    backgroundColor: 'rgba(0,0,0,0.2)',
    alignItems: 'center',
    justifyContent: 'center',
  },
  modalDetailImage: {
    width: 160,
    height: 160,
    borderRadius: 16,
    marginBottom: 10,
  },
  purityBadge: {
    position: 'absolute',
    top: 6,
    right: 6,
    paddingHorizontal: 7,
    paddingVertical: 3,
    borderRadius: 8,
  },
  purityBadgeText: {
    fontSize: 9,
    fontWeight: '900',
  },
  itemTitle: { fontSize: 13, fontWeight: '800', minHeight: 34 },
  itemBarcode: { fontSize: 11, fontWeight: '600', marginVertical: 2 },
  itemSnippet: { fontSize: 10, fontStyle: 'italic', marginBottom: 4 },
  weightRow: { flexDirection: 'row', justifyContent: 'space-between', marginTop: 6 },
  wtLabel: { fontSize: 9, fontWeight: '700' },
  wtVal: { fontSize: 12, fontWeight: '900' },
  centerContainer: { flex: 1, justifyContent: 'center', alignItems: 'center', paddingTop: 60 },
  loadingText: { fontSize: 13, marginTop: 12 },
  emptyContainer: { flex: 1, justifyContent: 'center', alignItems: 'center', padding: 24, paddingTop: 60 },
  emptyTitle: { fontSize: 17, fontWeight: '800', marginBottom: 6 },
  emptySubtitle: { fontSize: 13, textAlign: 'center', lineHeight: 18 },
  modalOverlay: { flex: 1, backgroundColor: 'rgba(0,0,0,0.8)', justifyContent: 'center', padding: 18 },
  filterCard: { padding: 20, borderRadius: 20, borderWidth: 1 },
  modalHeader: { flexDirection: 'row', justifyContent: 'space-between', alignItems: 'flex-start', marginBottom: 12 },
  modalTitle: { fontSize: 18, fontWeight: '900' },
  modalSub: { fontSize: 11, marginTop: 2 },
  closeBtn: { padding: 4 },
  filterSectionLabel: { fontSize: 11, fontWeight: '800', letterSpacing: 0.8, marginBottom: 6 },
  weightFilterRow: { flexDirection: 'row', gap: 10 },
  presetsWrap: { flexDirection: 'row', flexWrap: 'wrap', gap: 6, marginTop: 8 },
  presetChip: { paddingHorizontal: 10, paddingVertical: 5, borderRadius: 14, borderWidth: 1 },
  presetChipText: { fontSize: 11, fontWeight: '700' },
  tagsWrap: { flexDirection: 'row', flexWrap: 'wrap', gap: 6, marginTop: 8 },
  tagChip: { paddingHorizontal: 10, paddingVertical: 5, borderRadius: 14, borderWidth: 1 },
  tagChipText: { fontSize: 11, fontWeight: '700' },
  purityRow: { flexDirection: 'row', gap: 6, marginTop: 4 },
  purityChip: { flex: 1, paddingVertical: 8, borderRadius: 12, borderWidth: 1, alignItems: 'center' },
  purityChipText: { fontSize: 11, fontWeight: '800' },
  modalActionRow: { flexDirection: 'row', gap: 10, marginTop: 16 },
  detailCard: { padding: 20, borderRadius: 20 },
  detailIconBox: { width: 70, height: 70, borderRadius: 35, alignItems: 'center', justifyContent: 'center', marginBottom: 8 },
  detailTitle: { fontSize: 18, fontWeight: '900', textAlign: 'center' },
  detailBarcode: { fontSize: 12, fontWeight: '700', marginTop: 2 },
  detailGrid: { flexDirection: 'row', justifyContent: 'space-around', marginVertical: 12 },
  detailBox: { alignItems: 'center' },
  detailLabel: { fontSize: 10, fontWeight: '700' },
  detailVal: { fontSize: 14, fontWeight: '900', marginTop: 2 },
  descCard: { padding: 12, borderRadius: 12, borderWidth: 1, marginVertical: 8 },
  descHeading: { fontSize: 9, fontWeight: '800', letterSpacing: 0.6, marginBottom: 4 },
  detailDesc: { fontSize: 12, lineHeight: 17 },
  mrpRow: { flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center', marginVertical: 6, paddingHorizontal: 4 },
  mrpLabel: { fontSize: 12, fontWeight: '700' },
  mrpVal: { fontSize: 16, fontWeight: '900' },
});
