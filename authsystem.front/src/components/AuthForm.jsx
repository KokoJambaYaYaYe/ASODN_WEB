import React, {useState } from 'react';
import { Box, Button, Container, Paper, TextField, Typography, Stack } from '@mui/material';

export default function AuthForm() {

    const [mode, setMode] = useState('choose'); // 'choose', 'credentials', 'windows'
    const [login, setLogin] = useState('');
    const [password, setPassword] = useState('');

    const handleCredentialsSubmit = (e) => {
        e.preventDefault();

        const baseUrl = import.meta.env.VITE_API_URL;

        // Получаем текущий URL, в котором содержатся OIDC-параметры от OpenIddict
        const currentUrl = window.location.href;

        fetch(`${baseUrl}/authsystem_api/authcredentials/login`, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            credentials: 'include', // КРИТИЧНО: без этого кука авторизации не сохранится в браузере!
            body: JSON.stringify({
                login: login,
                password: password,
                returnUrl: currentUrl
            })
        })
            .then(async (res) => {
                if (res.ok) {
                    return res.json();
                }
                // Если бэкенд вернул ошибку (например, 400 BadRequest)
                const errorData = await res.json();
                throw new Error(errorData.error || 'Неверный логин или пароль');
            })
            .then((data) => {
                // Бэкенд успешного авторизовал пользователя и вернул URL.
                // Делаем редирект всей страницы обратно на эндпоинт /connect/authorize
                window.location.href = data.redirectUrl || currentUrl;
            })
            .catch((err) => {
                console.error('Ошибка авторизации по паролю:', err);
                alert(err.message);
            });
    };
    const handleWindowsAuth = () => {
        // 1. Берем базовый URL бэкенда из конфига Vite
        const baseUrl = import.meta.env.VITE_API_URL;

        // 2. Текущий URL (включая параметры OpenIddict, если они есть в строке)
        const currentUrl = window.location.href;

        // 3. Кодируем его, чтобы BFF знал, куда вернуть пользователя
        const encodedReturnUrl = encodeURIComponent(currentUrl);

        // 4. Делаем полноценный переход для срабатывания NTLM/Negotiate handshake
        window.location.href = `${baseUrl}/authsystem_api/authwindows/negotiate?returnUrl=${encodedReturnUrl}`;
    };

    return (
        <Container maxWidth="xs" sx={{ mt: 8 }}>
            <Paper elevation={3} sx={{ p: 4, display: 'flex', flexDirection: 'column', alignItems: 'center' }}>

                {mode === 'choose' && (
                    <>
                        <Typography component="h1" variant="h5" sx={{ mb: 3 }}>
                            Выберите вариант входа
                        </Typography>
                        <Stack spacing={2} sx={{ width: '100%' }}>
                            <Button
                                variant="contained"
                                fullWidth
                                onClick={() => setMode('credentials')}
                            >
                                Логин и пароль
                            </Button>
                            <Button
                                variant="outlined"
                                fullWidth
                                onClick={handleWindowsAuth}
                            >
                                Авторизация через Windows
                            </Button>
                        </Stack>
                    </>
                )}

                {mode === 'credentials' && (
                    <Box component="form" onSubmit={handleCredentialsSubmit} sx={{ width: '100%' }}>
                        <Typography component="h1" variant="h6" sx={{ mb: 2, textAlign: 'center' }}>
                            Вход по логину и паролю
                        </Typography>
                        <TextField
                            label="Логин"
                            variant="outlined"
                            fullWidth
                            margin="normal"
                            value={login}
                            onChange={(e) => setLogin(e.target.value)}
                            required
                        />
                        <TextField
                            label="Пароль"
                            type="password"
                            variant="outlined"
                            fullWidth
                            margin="normal"
                            value={password}
                            onChange={(e) => setPassword(e.target.value)}
                            required
                        />
                        <Button type="submit" variant="contained" fullWidth sx={{ mt: 2 }}>
                            Отправить
                        </Button>
                        <Button
                            variant="text"
                            fullWidth
                            sx={{ mt: 1 }}
                            onClick={() => setMode('choose')}
                        >
                            Назад
                        </Button>
                    </Box>
                )}

            </Paper>
        </Container>
    );
}
