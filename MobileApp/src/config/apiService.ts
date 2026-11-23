import axios from 'axios';
import AsyncStorage from '@react-native-async-storage/async-storage';
import { getServerUrl } from './serverConfig';

const AUTH_TOKEN_KEY = '@concept_jewellery_jwt_token';

export const getStoredToken = async (): Promise<string | null> => {
  try {
    return await AsyncStorage.getItem(AUTH_TOKEN_KEY);
  } catch {
    return null;
  }
};

export const setStoredToken = async (token: string): Promise<void> => {
  try {
    await AsyncStorage.setItem(AUTH_TOKEN_KEY, token);
  } catch {}
};

export const clearStoredToken = async (): Promise<void> => {
  try {
    await AsyncStorage.removeItem(AUTH_TOKEN_KEY);
  } catch {}
};

export const getApiBaseUrl = async (): Promise<string> => {
  return await getServerUrl();
};

export const getApiClient = async () => {
  const baseUrl = await getServerUrl();
  const token = await getStoredToken();

  const client = axios.create({
    baseURL: baseUrl,
    headers: {
      'Content-Type': 'application/json',
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
    },
    timeout: 10000,
  });

  return client;
};
