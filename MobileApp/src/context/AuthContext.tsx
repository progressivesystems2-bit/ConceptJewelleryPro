import React, { createContext, useContext, useState, useEffect } from 'react';
import AsyncStorage from '@react-native-async-storage/async-storage';
import { getApiClient, setStoredToken, clearStoredToken, getStoredToken } from '../config/apiService';

const USER_DATA_KEY = '@concept_jewellery_user_data';

export interface UserInfo {
  username: string;
  role: string;
  expiresAt: string;
}

interface AuthContextType {
  user: UserInfo | null;
  isLoading: boolean;
  login: (username: string, password: string) => Promise<{ success: boolean; message?: string }>;
  logout: () => Promise<void>;
}

const AuthContext = createContext<AuthContextType>({
  user: null,
  isLoading: true,
  login: async () => ({ success: false }),
  logout: async () => {},
});

export const AuthProvider: React.FC<{ children: React.ReactNode }> = ({ children }) => {
  const [user, setUser] = useState<UserInfo | null>(null);
  const [isLoading, setIsLoading] = useState<boolean>(true);

  useEffect(() => {
    checkSavedAuth();
  }, []);

  const checkSavedAuth = async () => {
    try {
      const token = await getStoredToken();
      const userData = await AsyncStorage.getItem(USER_DATA_KEY);
      if (token && userData) {
        setUser(JSON.parse(userData));
      }
    } catch (e) {
      console.warn('Error loading auth state', e);
    } finally {
      setIsLoading(false);
    }
  };

  const login = async (username: string, password: string) => {
    try {
      const client = await getApiClient();
      const response = await client.post('/api/Auth/login', { username, password });

      if (response.data && response.data.token) {
        const { token, username: userResp, role, expiresAt } = response.data;
        const userInfo: UserInfo = { username: userResp, role, expiresAt };

        await setStoredToken(token);
        await AsyncStorage.setItem(USER_DATA_KEY, JSON.stringify(userInfo));
        setUser(userInfo);
        return { success: true };
      }
      return { success: false, message: 'Invalid response from server' };
    } catch (error: any) {
      const msg = error.response?.data?.message || error.message || 'Login failed';
      return { success: false, message: msg };
    }
  };

  const logout = async () => {
    await clearStoredToken();
    await AsyncStorage.removeItem(USER_DATA_KEY);
    setUser(null);
  };

  return (
    <AuthContext.Provider value={{ user, isLoading, login, logout }}>
      {children}
    </AuthContext.Provider>
  );
};

export const useAuth = () => useContext(AuthContext);
