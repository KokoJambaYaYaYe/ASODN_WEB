import React from 'react';
import { Box, Paper, Typography, Button } from '@mui/material';

export default function ProfileScreenCom({ activeModule, loading, error, data, onLogout }) {
    if (activeModule !== 'user_data') return null;

    return (
        <Box className="profile-screen">
            <Paper elevation={3} className="profile-paper" style={{ padding: '20px', maxWidth: '500px', margin: '20px auto' }}>
                <Typography variant="h5" gutterBottom className="profile-title">
                    Личный кабинет пользователя
                </Typography>

                {loading && <Typography variant="body1">Загрузка данных...</Typography>}
                {error && <Typography variant="body1" color="error">Ошибка: {error}</Typography>}

                {!loading && !error && data && (
                    <>
                        <Typography variant="body1" className="profile-text-1" style={{ marginBottom: '8px' }}>
                            <strong>Пользователь (Login):</strong> {data.user}
                        </Typography>
                        <Typography variant="body1" className="profile-text-2" style={{ marginBottom: '8px' }}>
                            <strong>Роли в системе:</strong> {data.roles}
                        </Typography>
                        <Typography variant="body1" className="profile-method" style={{ color: '#555', fontStyle: 'italic' }}>
                            <strong>Способ авторизации:</strong> {data.loginMethod}
                        </Typography>
                        <Button variant="outlined" color="error" onClick={onLogout} style={{ marginTop: '15px' }}>
                            Выйти из системы
                        </Button>
                    </>
                )}
            </Paper>
        </Box>
    );
}
