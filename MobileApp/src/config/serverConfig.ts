import AsyncStorage from '@react-native-async-storage/async-storage';
import axios from 'axios';

const SERVER_URL_KEY = '@concept_jewellery_server_url';
export const DEFAULT_SERVER_URL = 'http://192.168.1.100:5000';

export const getServerUrl = async (): Promise<string> => {
  try {
    const saved = await AsyncStorage.getItem(SERVER_URL_KEY);
    return saved && saved.trim() !== '' ? saved.trim() : DEFAULT_SERVER_URL;
  } catch {
    return DEFAULT_SERVER_URL;
  }
};

export const saveServerUrl = async (url: string): Promise<void> => {
  let formatted = url.trim();
  if (!formatted.startsWith('http://') && !formatted.startsWith('https://')) {
    formatted = `http://${formatted}`;
  }
  // Remove trailing slash if any
  formatted = formatted.replace(/\/+$/, '');
  await AsyncStorage.setItem(SERVER_URL_KEY, formatted);
};

export interface ConnectionTestResult {
  success: boolean;
  message: string;
  responseTimeMs?: number;
}

export const testServerConnection = async (targetUrl: string): Promise<ConnectionTestResult> => {
  let formatted = targetUrl.trim();
  if (!formatted.startsWith('http://') && !formatted.startsWith('https://')) {
    formatted = `http://${formatted}`;
  }
  formatted = formatted.replace(/\/+$/, '');

  const startTime = Date.now();
  try {
    // Try pinging Swagger UI or Auth Endpoint or direct host
    const response = await axios.get(`${formatted}/api/auth/login`, {
      timeout: 4000,
      validateStatus: (status) => status < 500, // 400, 401, 405 mean server IS reachable!
    });
    const responseTimeMs = Date.now() - startTime;
    return {
      success: true,
      message: `Connected successfully (${responseTimeMs}ms)`,
      responseTimeMs,
    };
  } catch (error: any) {
    if (error.code === 'ECONNABORTED') {
      return { success: false, message: 'Connection timed out (4s limit)' };
    }
    return {
      success: false,
      message: error.message || 'Unable to reach host. Check IP & port.',
    };
  }
};
