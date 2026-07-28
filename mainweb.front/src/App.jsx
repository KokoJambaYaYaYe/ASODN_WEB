import React, { useState, Suspense, lazy } from 'react';
import { AppBar, Toolbar, Typography, Box, Button, Paper, CssBaseline } from '@mui/material';
import BarChartIcon from '@mui/icons-material/BarChart';
import HomeIcon from '@mui/icons-material/Home';
import AccountCircleIcon from '@mui/icons-material/AccountCircle';
import './App.css';
import { RemoteErrorBoundary } from './RemoteErrorBoundary';

// Локальные компоненты (вынесенные экраны)
import HeaderCom from './main_page/components/HeaderCom';
import HubScreenCom from './main_page/components/HubScreenCom';
import ProfileScreenCom from './main_page/components/ProfileScreenCom';
import FloatingHomeButtonCom from './main_page/components/FloatingHomeButtonCom';

// Ленивая загрузка удаленного модуля
const RemoteSvodApp = lazy(() => import('svod_reports/App'));
const RemoteAuthApp = lazy(() =>
    import('auth_module/components').then(module => ({ default: module.AuthForm }))
);

function App() {

    // ВАЖНО: Стартовое состояние строго 'hub'
    const [activeModule, setActiveModule] = useState('hub');
    const [isLogined, setIsLogined] = useState(false);
    const [hasModuleError, setHasModuleError] = useState(false);

    // Новые состояния для данных профиля
    const [profileData, setProfileData] = useState(null);
    const [profileLoading, setProfileLoading] = useState(false);
    const [profileError, setProfileError] = useState(null);

    const handleNavigate = (moduleName) => {
        // Сбрасываем ошибку микрофронтендов при уходе на главную
        if (moduleName === 'hub') {
            setHasModuleError(false);
        }

        // Переключаем экран
        setActiveModule(moduleName);

        // Если пользователь переходит в профиль — загружаем данные с бэкенда
        if (moduleName === 'user_data') {

                setProfileLoading(true);
                setProfileError(null);

                const baseUrl = import.meta.env.VITE_API_URL || 'https://localhost:7210';

                fetch(`${baseUrl}/api/profile/info`, {
                    method: 'GET',
                    credentials: 'include' // КРИТИЧНО для передачи сессионной куки
                })
                    .then((res) => {
                        if (res.ok) {
                            // Если бэкенд ответил 200 OK, значит кука валидна, пользователь авторизован!
                            setIsLogined(true);
                            return res.json();
                        } else {
                            // Если 401 Unauthorized, значит куки нет — отправляем на форму входа
                            setIsLogined(false);
                            setActiveModule('auth_module');
                        }
                        
                    })
                    .then((data) => {
                        setProfileData(data);
                        setProfileLoading(false);
                    })
                    .catch((err) => {
                        console.error(err);
                        setProfileError(err.message);
                        setProfileLoading(false);
                    });
                      
        }
    };
    const handleLogoutViaRedirect = () => {
        // 1. Берем базовый URL бэкенда
        const baseUrl = import.meta.env.VITE_API_URL || "https://localhost:7210";

        // 2. Очищаем локальные токены перед уходом (если они есть)
        localStorage.removeItem('access_token');
        localStorage.removeItem('id_token');

        // 3. Формируем URL возврата (после логаута вернем пользователя на корень фронтенда)
        const currentOrigin = window.location.origin;
        const encodedReturnUrl = encodeURIComponent(currentOrigin);

        setIsLogined(false);

        // 4. Делаем полноценный переход для очистки сессий на стороне сервера
        window.location.href = `${baseUrl}/connect/logout?returnUrl=${encodedReturnUrl}`;
    };
    // Ваш старый хэндлер теперь просто вызывает новый универсальный
    const handleNavigateToHub = () => handleNavigate('hub');


    return (
        <Box className="app-container">
            <CssBaseline />

            {/* 1. Навигационная панель */}
            <HeaderCom activeModule={activeModule} onNavigate={handleNavigate} />

            {/* Центральный контент */}
            <Box component="main" className={`main-content ${activeModule === 'hub' ? 'p-hub' : 'p-module'}`}>

                {/* 2. Экран выбора систем (Хаб) */}
                <HubScreenCom activeModule={activeModule} onNavigate={handleNavigate} />

                {/* 3. Микрофронтенд отчетов */}
                {activeModule === 'svod_reports' && (
                    <Box className="reports-screen">
                        <RemoteErrorBoundary
                            onErrorStateChange={setHasModuleError}
                            onNavigateToHub={() => handleNavigateToHub()} // Прокидываем функцию навигации внутрь класса
                        >
                            <Suspense fallback={<Box className="loading-box">Загрузка отчетов...</Box>}>
                                <RemoteSvodApp />
                            </Suspense>
                        </RemoteErrorBoundary>
                    </Box>
                )}

                {/* 4. Личный кабинет пользователя */}
                <ProfileScreenCom
                    activeModule={activeModule}
                    loading={profileLoading}
                    error={profileError}
                    data={profileData}
                    onLogout={handleLogoutViaRedirect}
                />

                {/* 5. Микрофронтенд авторизации */}
                {activeModule === 'auth_module' && (
                    <Box className="auth_module-screen">
                        <RemoteErrorBoundary onErrorStateChange={setHasModuleError}>
                            <Suspense fallback={<Box className="loading-box">Загрузка формы авторизации...</Box>}>
                                <RemoteAuthApp />
                            </Suspense>
                        </RemoteErrorBoundary>
                    </Box>
                )}
            </Box>

            {/* 6. Плавающая кнопка возврата */}
            <FloatingHomeButtonCom
                activeModule={activeModule}
                hasError={hasModuleError}
                onNavigate={handleNavigate}
            />
        </Box>
    );
}

export default App;
